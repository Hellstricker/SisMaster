using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class DefinirConfrontoCommandHandler : IRequestHandler<DefinirConfrontoCommand, ValidationResult>
{
    private readonly IFaseRepository _faseRepository;
    private readonly IJogoRepository _jogoRepository;
    private readonly IEquipeRepository _equipeRepository;

    public DefinirConfrontoCommandHandler(IFaseRepository faseRepository, IJogoRepository jogoRepository, IEquipeRepository equipeRepository)
    {
        _faseRepository = faseRepository;
        _jogoRepository = jogoRepository;
        _equipeRepository = equipeRepository;
    }

    public async Task<ValidationResult> Handle(DefinirConfrontoCommand request, CancellationToken cancellationToken)
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

            var temporada = fase.TemporadaCategoria.Temporada;
            temporada.ValidarPermiteCadastrarFases();
            if (temporada.TabelaJogosGerada)
                throw new DomainException("A tabela de jogos já foi gerada: os cruzamentos não podem mais ser alterados");

            if (fase.Tipo != TipoFase.MataMata)
                throw new DomainException("Só fases de mata-mata têm confrontos");
            if (fase.NumeroConfrontos is null || request.Numero > fase.NumeroConfrontos)
                throw new DomainException($"A fase tem {fase.NumeroConfrontos} confronto(s)");

            var fases = await _faseRepository.ListarPorTemporadaCategoriaAsync(fase.TemporadaCategoriaId, cancellationToken);
            var confrontosDaCategoria = await _jogoRepository.ListarConfrontosDaCategoriaAsync(fase.TemporadaCategoriaId, cancellationToken);

            var origemA = request.OrigemA.ParaReferencia();
            var origemB = request.OrigemB.ParaReferencia();
            await ValidarOrigemAsync(origemA, fase, fases, confrontosDaCategoria, cancellationToken);
            await ValidarOrigemAsync(origemB, fase, fases, confrontosDaCategoria, cancellationToken);

            var confrontos = await _jogoRepository.ListarConfrontosDaFaseAsync(fase.Id, cancellationToken);
            var existente = confrontos.FirstOrDefault(c => c.Numero == request.Numero);
            if (existente is null)
                _jogoRepository.AdicionarConfronto(new Confronto(fase.Id, request.Numero, request.Nome, origemA, origemB));
            else
                existente.Definir(request.Nome, origemA, origemB);

            await _jogoRepository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }

    private async Task ValidarOrigemAsync(ReferenciaEquipe origem, Fase fase, List<Fase> fasesDaCategoria,
        IReadOnlyList<Confronto> confrontosDaCategoria, CancellationToken ct)
    {
        switch (origem.Tipo)
        {
            case TipoReferencia.Equipe:
            {
                var equipe = await _equipeRepository.ObterPorIdAsync(origem.EquipeId!.Value, ct);
                if (equipe is null || equipe.TemporadaCategoriaId != fase.TemporadaCategoriaId)
                    throw new DomainException("A equipe precisa ser da mesma categoria da fase");
                break;
            }

            case TipoReferencia.Colocacao:
            {
                var origemFase = fasesDaCategoria.FirstOrDefault(f => f.Id == origem.FaseId)
                    ?? throw new DomainException("A fase da colocação precisa ser da mesma categoria");
                if (origemFase.Ordem >= fase.Ordem)
                    throw new DomainException("A fase da colocação precisa vir antes desta na sequência");
                if (origemFase.Tipo == TipoFase.MataMata)
                    throw new DomainException("Colocação só existe em fases de pontos corridos ou grupos; use vencedor/perdedor do confronto");
                if (origemFase.Tipo == TipoFase.Grupos)
                {
                    if (origem.GrupoOrdem is null || origem.GrupoOrdem > origemFase.NumeroGrupos)
                        throw new DomainException($"Informe o grupo (1 a {origemFase.NumeroGrupos}) da colocação");
                }
                else if (origem.GrupoOrdem is not null)
                {
                    throw new DomainException("Pontos corridos não têm grupos");
                }
                break;
            }

            default:
            {
                var confronto = confrontosDaCategoria.FirstOrDefault(c => c.Id == origem.ConfrontoOrigemId)
                    ?? throw new DomainException("O confronto de origem precisa ser da mesma categoria (e já estar definido)");
                var faseDoConfronto = fasesDaCategoria.First(f => f.Id == confronto.FaseId);
                if (faseDoConfronto.Ordem >= fase.Ordem)
                    throw new DomainException("O confronto de origem precisa estar numa fase anterior a esta");
                break;
            }
        }
    }
}
