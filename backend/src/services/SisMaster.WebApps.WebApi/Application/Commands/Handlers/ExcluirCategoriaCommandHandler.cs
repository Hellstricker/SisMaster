using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class ExcluirCategoriaCommandHandler : IRequestHandler<ExcluirCategoriaCommand, ValidationResult>
{
    private readonly ICategoriaRepository _repository;

    public ExcluirCategoriaCommandHandler(ICategoriaRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(ExcluirCategoriaCommand request, CancellationToken cancellationToken)
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

            // TemporadaCategoria referencia a categoria (FK restrita): enquanto alguma temporada a usa, não exclui.
            var uso = await _repository.ContarUsoAsync(categoria.Id, cancellationToken);
            if (uso > 0)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty,
                    $"Não é possível excluir: a categoria está em uso em {uso} temporada(s)"));
                return request.ValidationResult;
            }

            _repository.Remover(categoria);
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
