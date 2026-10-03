using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Time;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class CriarTimeCommandHandler : IRequestHandler<CriarTimeCommand, ValidationResult>
{
    private readonly ITimeRepository _timeRepository;

    public CriarTimeCommandHandler(ITimeRepository timeRepository)
    {
        _timeRepository = timeRepository;
    }

    public async Task<ValidationResult> Handle(CriarTimeCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var time = new Domain.Time.Time(request.AssociacaoId, request.Nome, request.Sigla);
            _timeRepository.Adicionar(time);
            await _timeRepository.UnitOfWork.Commit();

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
