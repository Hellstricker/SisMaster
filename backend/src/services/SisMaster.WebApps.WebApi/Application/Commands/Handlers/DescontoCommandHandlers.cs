using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class DefinirDescontoCommandHandler : IRequestHandler<DefinirDescontoCommand, ValidationResult>
{
    private readonly ITemporadaRepository _repository;

    public DefinirDescontoCommandHandler(ITemporadaRepository repository) => _repository = repository;

    public async Task<ValidationResult> Handle(DefinirDescontoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var temporada = await _repository.ObterPorIdAsync(request.TemporadaId, cancellationToken);
            if (temporada is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Temporada não encontrada"));
                return request.ValidationResult;
            }

            var (desconto, novo) = temporada.DefinirDesconto(request.TemporadaCategoriaIds, request.Tipo, request.Valor);
            if (novo) _repository.AdicionarDesconto(desconto);
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

public class RemoverDescontoCommandHandler : IRequestHandler<RemoverDescontoCommand, ValidationResult>
{
    private readonly ITemporadaRepository _repository;

    public RemoverDescontoCommandHandler(ITemporadaRepository repository) => _repository = repository;

    public async Task<ValidationResult> Handle(RemoverDescontoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var temporada = await _repository.ObterPorIdAsync(request.TemporadaId, cancellationToken);
            if (temporada is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Temporada não encontrada"));
                return request.ValidationResult;
            }

            var removido = temporada.RemoverDesconto(request.DescontoId);
            _repository.RemoverDesconto(removido);
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
