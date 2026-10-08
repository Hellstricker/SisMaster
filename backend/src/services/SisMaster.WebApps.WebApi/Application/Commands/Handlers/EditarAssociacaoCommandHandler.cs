using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class EditarAssociacaoCommandHandler : IRequestHandler<EditarAssociacaoCommand, ValidationResult>
{
    private readonly IAssociacaoRepository _repository;

    public EditarAssociacaoCommandHandler(IAssociacaoRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(EditarAssociacaoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var associacao = await _repository.ObterPorIdAsync(request.AssociacaoId, cancellationToken);
            if (associacao is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Associação não encontrada"));
                return request.ValidationResult;
            }

            associacao.Editar(request.Nome, request.Sigla, request.Uf);
            _repository.Atualizar(associacao);
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
