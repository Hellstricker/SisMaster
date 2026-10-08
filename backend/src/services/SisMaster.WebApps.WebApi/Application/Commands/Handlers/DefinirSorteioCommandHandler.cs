using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class DefinirSorteioCommandHandler : IRequestHandler<DefinirSorteioCommand, ValidationResult>
{
    private readonly IFaseRepository _faseRepository;
    private readonly IJogoRepository _jogoRepository;
    private readonly BonificacaoDosJogos _bonificacao;
    private readonly AvancoDaTemporada _avanco;

    public DefinirSorteioCommandHandler(IFaseRepository faseRepository, IJogoRepository jogoRepository, BonificacaoDosJogos bonificacao, AvancoDaTemporada avanco)
    {
        _faseRepository = faseRepository;
        _jogoRepository = jogoRepository;
        _bonificacao = bonificacao;
        _avanco = avanco;
    }

    public async Task<ValidationResult> Handle(DefinirSorteioCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var fase = await _faseRepository.ObterPorIdAsync(request.FaseId, cancellationToken);
            if (fase is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Fase não encontrada"));
                return request.ValidationResult;
            }
            if (fase.Tipo == TipoFase.MataMata)
                throw new DomainException("Mata-mata não tem tabela de classificação: não há sorteio de desempate");

            var temporada = fase.TemporadaCategoria.Temporada;
            if (!temporada.TabelaJogosGerada)
                throw new DomainException("A tabela de jogos ainda não foi gerada");

            var tabela = await _jogoRepository.ObterTabelaDaFaseAsync(fase.Id, cancellationToken);
            Guid? grupoId = null;
            if (fase.Tipo == TipoFase.Grupos)
            {
                var grupo = tabela.Grupos.FirstOrDefault(g => g.Ordem == request.GrupoOrdem)
                    ?? throw new DomainException("Informe o grupo do empate");
                grupoId = grupo.Id;
            }

            var vagas = tabela.Vagas.Where(v => v.GrupoId == grupoId).ToList();
            var jogos = tabela.Jogos.Where(j => j.GrupoId == grupoId).ToList();
            if (!TabelaDeClassificacao.Definitiva(jogos))
                throw new DomainException("O empate só pode ser decidido por sorteio depois de terminados todos os jogos da tabela");

            // Quem está de fato empatado sem critério (a classificação sem sorteio algum).
            var bonificacoes = await _bonificacao.CalcularAsync(jogos, [fase], cancellationToken);
            var linhas = TabelaDeClassificacao.Calcular(vagas, jogos, v => v.EquipeId?.ToString() ?? v.Id.ToString(), bonificacoes, null);
            var empatados = linhas.Where(l => l.EmpatePorSorteio).Select(l => l.Participante.Id).ToHashSet();
            if (empatados.Count == 0)
                throw new DomainException("Não há empate sem critério nesta tabela");
            if (!empatados.SetEquals(request.Ordem))
                throw new DomainException("A ordem do sorteio deve conter exatamente as equipes empatadas, uma vez cada");

            var existente = await _jogoRepository.ObterSorteioAsync(fase.Id, grupoId, cancellationToken);
            if (existente is not null)
            {
                if (await _jogoRepository.ExisteVagaResolvidaPelaColocacaoAsync(fase.Id, cancellationToken))
                    throw new DomainException("O sorteio não pode mais ser alterado: já há equipes das fases seguintes definidas a partir desta classificação");
                existente.Redefinir(request.Ordem);
            }
            else
            {
                _jogoRepository.AdicionarSorteio(new SorteioDeDesempate(fase.Id, grupoId, request.Ordem));
            }
            await _jogoRepository.UnitOfWork.Commit();

            // O desempate pode destravar as vagas das fases seguintes.
            await _avanco.ExecutarAsync(temporada.Id, cancellationToken);
            await _jogoRepository.UnitOfWork.Commit();
            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
