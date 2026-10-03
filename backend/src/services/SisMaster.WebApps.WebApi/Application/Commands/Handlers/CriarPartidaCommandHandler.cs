using FluentValidation.Results;
using MediatR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Partida;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class CriarPartidaCommandHandler : IRequestHandler<CriarPartidaCommand, ValidationResult>
{
    private readonly IPartidaRepository _partidaRepository;

    public CriarPartidaCommandHandler(IPartidaRepository partidaRepository)
    {
        _partidaRepository = partidaRepository;
    }

    public async Task<ValidationResult> Handle(CriarPartidaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var partida = new Partida(request.CampeonatoId, request.TimeCasaId, request.TimeVisitanteId);
            _partidaRepository.Adicionar(partida);
            await _partidaRepository.UnitOfWork.Commit();

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
