using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class CriarCampeonatoCommandHandler : IRequestHandler<CriarCampeonatoCommand, ValidationResult>
{
    private readonly IAssociacaoRepository _repository;

    public CriarCampeonatoCommandHandler(IAssociacaoRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(CriarCampeonatoCommand request, CancellationToken cancellationToken)
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

            // Filho novo de agregado já carregado: Add explícito (Update o trataria como existente e geraria UPDATE).
            var campeonato = associacao.AdicionarCampeonato(request.Nome);
            _repository.AdicionarCampeonato(campeonato);
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
