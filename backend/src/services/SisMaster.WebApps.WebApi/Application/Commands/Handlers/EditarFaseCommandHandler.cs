using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class EditarFaseCommandHandler : IRequestHandler<EditarFaseCommand, ValidationResult>
{
    private readonly IFaseRepository _repository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly IJogoRepository _jogoRepository;

    public EditarFaseCommandHandler(IFaseRepository repository, IEquipeRepository equipeRepository, IJogoRepository jogoRepository)
    {
        _repository = repository;
        _equipeRepository = equipeRepository;
        _jogoRepository = jogoRepository;
    }

    public async Task<ValidationResult> Handle(EditarFaseCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var fase = await _repository.ObterPorIdAsync(request.FaseId, cancellationToken);
            if (fase is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Fase não encontrada"));
                return request.ValidationResult;
            }

            var temporada = fase.TemporadaCategoria.Temporada;
            temporada.ValidarPermiteCadastrarFases();

            if (request.FaseAnteriorId is not null && request.FaseAnteriorId != fase.FaseAnteriorId)
            {
                var fases = await _repository.ListarPorTemporadaCategoriaAsync(fase.TemporadaCategoriaId, cancellationToken);
                var anterior = SequenciaDeFases.ObterAnterior(fases, request.FaseAnteriorId.Value, fase.Id);
                if (anterior.Ordem >= fase.Ordem)
                    throw new DomainException("A fase anterior precisa estar antes desta na sequência. Reordene primeiro");
            }

            var equipes = (await _equipeRepository.ContarPorCategoriaAsync(temporada.Id, cancellationToken)).GetValueOrDefault(fase.TemporadaCategoriaId);

            fase.Editar(temporada.CadastroFasesEncerrado, request.Nome, request.Tipo, request.Estrutura(), request.FaseAnteriorId, equipes);

            // Confrontos que sobraram (a fase deixou de ser mata-mata ou ficou com menos confrontos) saem junto.
            var excedentes = (await _jogoRepository.ListarConfrontosDaFaseAsync(fase.Id, cancellationToken))
                .Where(c => fase.Tipo != TipoFase.MataMata || c.Numero > fase.NumeroConfrontos).ToList();
            if (excedentes.Count > 0)
            {
                if (await _jogoRepository.ExisteReferenciaAosConfrontosAsync(excedentes.Select(c => c.Id).ToList(), cancellationToken))
                    throw new DomainException("Não é possível reduzir os confrontos: outro cruzamento usa um deles como origem");
                foreach (var c in excedentes) _jogoRepository.RemoverConfronto(c);
            }

            await _repository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
