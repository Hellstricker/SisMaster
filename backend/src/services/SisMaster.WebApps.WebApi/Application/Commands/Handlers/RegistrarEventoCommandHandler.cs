using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;
using SisMaster.WebApps.WebApi.Hubs;
using SisMaster.WebApps.WebApi.Hubs.Dtos;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class RegistrarEventoCommandHandler : IRequestHandler<RegistrarEventoCommand, ValidationResult>
{
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IHubContext<SumulaHub> _hub;

    public RegistrarEventoCommandHandler(ISumulaRepository sumulaRepository, IHubContext<SumulaHub> hub)
    {
        _sumulaRepository = sumulaRepository;
        _hub = hub;
    }

    public async Task<ValidationResult> Handle(RegistrarEventoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        var sumula = await _sumulaRepository.ObterPorIdAsync(request.PartidaId, cancellationToken);
        if (sumula is null)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure("PartidaId", "Súmula não encontrada"));
            return request.ValidationResult;
        }

        // Reenvio do mesmo lançamento (a resposta se perdeu na rede): já está gravado.
        if (request.EventoId.HasValue && sumula.Eventos.Any(e => e.Id == request.EventoId.Value))
            return request.ValidationResult;

        try
        {
            // O jogador precisa estar relacionado nesta súmula.
            var evento = sumula.RegistrarEvento(request.JogadorId, request.Tipo, request.TempoJogoSegundos,
                request.EventoId, request.Periodo.HasValue ? (Periodo)request.Periodo.Value : null);

            _sumulaRepository.AdicionarEvento(evento);
            await _sumulaRepository.UnitOfWork.Commit();

            var dto = new EventoRegistradoDto
            {
                EventoId = evento.Id,
                PartidaId = sumula.Id,
                JogadorId = request.JogadorId,
                Tipo = request.Tipo.ToString(),
                PeriodoAtual = (int)sumula.PeriodoAtual,
                TempoJogoSegundos = request.TempoJogoSegundos,
                PlacarCasa = sumula.PlacarCasa,
                PlacarVisitante = sumula.PlacarVisitante,
                FaltasCasa = sumula.FaltasDeEquipeParaExibir(LadoTime.Casa),
                FaltasVisitante = sumula.FaltasDeEquipeParaExibir(LadoTime.Visitante)
            };

            await _hub.Clients.Group($"partida-{sumula.Id}")
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
