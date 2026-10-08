using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class EncerrarInscricoesCommandHandler : IRequestHandler<EncerrarInscricoesCommand, ValidationResult>
{
    private readonly ICampeonatoRepository _repository;

    public EncerrarInscricoesCommandHandler(ICampeonatoRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(EncerrarInscricoesCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var campeonato = await _repository.ObterPorIdAsync(request.CampeonatoId, cancellationToken);
            if (campeonato is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Campeonato não encontrado"));
                return request.ValidationResult;
            }

            var temporada = campeonato.ObterTemporada(request.TemporadaId);
            if (temporada is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Temporada não encontrada"));
                return request.ValidationResult;
            }

            temporada.EncerrarInscricoes();
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
