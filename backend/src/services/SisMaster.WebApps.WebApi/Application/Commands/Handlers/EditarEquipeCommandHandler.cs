using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class EditarEquipeCommandHandler : IRequestHandler<EditarEquipeCommand, ValidationResult>
{
    private readonly IEquipeRepository _repository;

    public EditarEquipeCommandHandler(IEquipeRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(EditarEquipeCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var equipe = await _repository.ObterPorIdAsync(request.EquipeId, cancellationToken);
            if (equipe is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Equipe não encontrada"));
                return request.ValidationResult;
            }

            equipe.TemporadaCategoria.Temporada.ValidarPermiteFormarEquipes();

            if (await _repository.ExisteNomeNaTemporadaAsync(equipe.TemporadaCategoria.TemporadaId, request.Nome.Trim(), equipe.Id, cancellationToken))
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Já existe uma equipe com esse nome nesta temporada"));
                return request.ValidationResult;
            }

            equipe.Editar(request.Nome, request.Cor);
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
