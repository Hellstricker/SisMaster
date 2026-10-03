using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Partida;
using SisMaster.WebApps.WebApi.Domain.Time;
using SisMaster.WebApps.WebApi.Hubs;
using SisMaster.WebApps.WebApi.Hubs.Dtos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class RegistrarEventoCommandHandler : IRequestHandler<RegistrarEventoCommand, ValidationResult>
{
    private readonly IPartidaRepository _partidaRepository;
    private readonly ITimeRepository _timeRepository;
    private readonly IHubContext<SumulaHub> _hub;

    public RegistrarEventoCommandHandler(
        IPartidaRepository partidaRepository,
        ITimeRepository timeRepository,
        IHubContext<SumulaHub> hub)
    {
        _partidaRepository = partidaRepository;
        _timeRepository = timeRepository;
        _hub = hub;
    }

    public async Task<ValidationResult> Handle(RegistrarEventoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        var partida = await _partidaRepository.ObterPorIdAsync(request.PartidaId, cancellationToken);
        if (partida is null)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure("PartidaId", "Partida não encontrada"));
            return request.ValidationResult;
        }

        var jogador = await _timeRepository.ObterJogadorPorIdAsync(request.JogadorId, cancellationToken);
        if (jogador is null)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure("JogadorId", "Jogador não encontrado"));
            return request.ValidationResult;
        }

        try
        {
            var evento = partida.RegistrarEvento(request.JogadorId, jogador.TimeId, request.Tipo, request.TempoJogoSegundos);

            _partidaRepository.AdicionarEvento(evento);
            await _partidaRepository.UnitOfWork.Commit();

            var dto = new EventoRegistradoDto
            {
                EventoId = evento.Id,
                PartidaId = partida.Id,
                JogadorId = request.JogadorId,
                Tipo = request.Tipo.ToString(),
                PeriodoAtual = (int)partida.PeriodoAtual,
                TempoJogoSegundos = request.TempoJogoSegundos,
                PlacarCasa = partida.PlacarCasa,
                PlacarVisitante = partida.PlacarVisitante,
                FaltasCasa = partida.FaltasCasa,
                FaltasVisitante = partida.FaltasVisitante
            };

            await _hub.Clients.Group($"partida-{partida.Id}")
                .SendAsync("EventoRegistrado", dto, cancellationToken);

            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
