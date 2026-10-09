using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class AlterarDatasTemporadaCommandHandler : IRequestHandler<AlterarDatasTemporadaCommand, ValidationResult>
{
    private readonly ITemporadaRepository _repository;

    public AlterarDatasTemporadaCommandHandler(ITemporadaRepository repository)
    {
        _repository = repository;
    }

    public async Task<ValidationResult> Handle(AlterarDatasTemporadaCommand request, CancellationToken cancellationToken)
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

            temporada.AlterarDatas(request.DataInicioInscricoes, request.DataFimInscricoes, request.DataInicioCompeticao, request.DataFimCompeticao);
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
