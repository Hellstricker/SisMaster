using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class EditarCategoriaCommandHandler : IRequestHandler<EditarCategoriaCommand, ValidationResult>
{
    private readonly ICategoriaRepository _repository;

    public EditarCategoriaCommandHandler(ICategoriaRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(EditarCategoriaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var categoria = await _repository.ObterPorIdAsync(request.CategoriaId, cancellationToken);
            if (categoria is null || categoria.AssociacaoId != request.AssociacaoId)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Categoria não encontrada"));
                return request.ValidationResult;
            }

            var nome = request.Nome.Trim();
            if (!string.Equals(categoria.Nome, nome, StringComparison.Ordinal)
                && await _repository.ExisteComNomeAsync(request.AssociacaoId, nome, cancellationToken))
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Já existe uma categoria com esse nome nesta associação"));
                return request.ValidationResult;
            }

            categoria.Editar(nome, request.IdadeMinima, request.Sexo,
                request.MinimoPeriodosEmQuadra, request.MinimoPeriodosForaQuadra, request.AceitaAbaixoIdadeMinima);
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
