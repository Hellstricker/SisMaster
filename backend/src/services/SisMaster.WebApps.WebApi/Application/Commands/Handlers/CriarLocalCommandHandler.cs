using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class CriarLocalCommandHandler : IRequestHandler<CriarLocalCommand, ValidationResult>
{
    private readonly IAssociacaoRepository _associacaoRepository;
    private readonly ILocalRepository _localRepository;

    public CriarLocalCommandHandler(IAssociacaoRepository associacaoRepository, ILocalRepository localRepository)
    {
        _associacaoRepository = associacaoRepository;
        _localRepository = localRepository;
    }

    public async Task<ValidationResult> Handle(CriarLocalCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var associacao = await _associacaoRepository.ObterPorIdAsync(request.AssociacaoId, cancellationToken);
            if (associacao is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Associação não encontrada"));
                return request.ValidationResult;
            }

            var local = new Local(request.AssociacaoId, request.Nome, request.Cidade, request.Estado);
            if (await _localRepository.ExisteAsync(request.AssociacaoId, local.Nome, local.Cidade, null, cancellationToken))
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Já existe um local com esse nome nessa cidade"));
                return request.ValidationResult;
            }

            _localRepository.Adicionar(local);
            await _localRepository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
