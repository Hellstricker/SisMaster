using FluentValidation.Results;
using MediatR;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class AlterarStatusAssociacaoCommandHandler : IRequestHandler<AlterarStatusAssociacaoCommand, ValidationResult>
{
    private readonly IAssociacaoRepository _repository;

    public AlterarStatusAssociacaoCommandHandler(IAssociacaoRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(AlterarStatusAssociacaoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        var associacao = await _repository.ObterPorIdAsync(request.AssociacaoId, cancellationToken);
        if (associacao is null)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Associação não encontrada"));
            return request.ValidationResult;
        }

        if (request.Ativa) associacao.Ativar();
        else associacao.Desativar();

        _repository.Atualizar(associacao);
        await _repository.UnitOfWork.Commit();

        return request.ValidationResult;
    }
}
