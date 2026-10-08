using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class GerarTabelaJogosCommandHandler : IRequestHandler<GerarTabelaJogosCommand, ValidationResult>
{
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly IFaseRepository _faseRepository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly IJogoRepository _jogoRepository;

    public GerarTabelaJogosCommandHandler(ITemporadaRepository temporadaRepository, IFaseRepository faseRepository,
        IEquipeRepository equipeRepository, IJogoRepository jogoRepository)
    {
        _temporadaRepository = temporadaRepository;
        _faseRepository = faseRepository;
        _equipeRepository = equipeRepository;
        _jogoRepository = jogoRepository;
    }

    public async Task<ValidationResult> Handle(GerarTabelaJogosCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var temporada = await _temporadaRepository.ObterPorIdAsync(request.TemporadaId, cancellationToken);
            if (temporada is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Temporada não encontrada"));
                return request.ValidationResult;
            }

            temporada.ValidarPodeGerarTabelaJogos();

            var fases = await _faseRepository.ListarPorTemporadaAsync(temporada.Id, cancellationToken);
            var confrontos = await _jogoRepository.ListarConfrontosDaTemporadaAsync(temporada.Id, cancellationToken);
            var equipes = await _equipeRepository.ListarPorTemporadaAsync(temporada.Id, cancellationToken);

            var categorias = temporada.Categorias.Select(c => new CategoriaParaGerar(
                c.Id, c.Nome,
                equipes.Where(e => e.TemporadaCategoriaId == c.Id).ToList(),
                fases.Where(f => f.TemporadaCategoriaId == c.Id).ToList(),
                confrontos.Where(x => fases.Any(f => f.Id == x.FaseId && f.TemporadaCategoriaId == c.Id)).ToList()));

            var tabela = ServicoGeracaoJogos.Gerar(temporada.Id, categorias);
            if (tabela.Jogos.Count == 0)
                throw new DomainException("Nenhum jogo a gerar: cadastre as fases das categorias");

            _jogoRepository.AdicionarTabela(tabela);
            temporada.MarcarTabelaJogosGerada();
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
