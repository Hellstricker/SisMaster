using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class CriarAssociacaoCommandHandler : IRequestHandler<CriarAssociacaoCommand, ValidationResult>
{
    private readonly IAssociacaoRepository _repository;

    public CriarAssociacaoCommandHandler(IAssociacaoRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(CriarAssociacaoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var associacao = new Associacao(request.Nome, request.Sigla, request.Uf);
            _repository.Adicionar(associacao);
            await _repository.UnitOfWork.Commit();

            request.ValidationResult.Errors.Clear();
            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
