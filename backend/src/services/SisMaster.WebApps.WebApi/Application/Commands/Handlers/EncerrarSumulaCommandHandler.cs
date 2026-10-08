using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Hubs;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class EncerrarSumulaCommandHandler : IRequestHandler<EncerrarSumulaCommand, ValidationResult>
{
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IJogoRepository _jogoRepository;
    private readonly IHubContext<SumulaHub> _hub;
    private readonly AvancoDaTemporada _avanco;

    public EncerrarSumulaCommandHandler(ISumulaRepository sumulaRepository, IJogoRepository jogoRepository, IHubContext<SumulaHub> hub, AvancoDaTemporada avanco)
    {
        _sumulaRepository = sumulaRepository;
        _jogoRepository = jogoRepository;
        _hub = hub;
        _avanco = avanco;
    }

    public async Task<ValidationResult> Handle(EncerrarSumulaCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        try
        {
            var sumula = await _sumulaRepository.ObterPorIdAsync(request.SumulaId, cancellationToken);
            if (sumula is null)
            {
                request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, "Súmula não encontrada"));
                return request.ValidationResult;
            }
            var jogo = await _jogoRepository.ObterPorIdAsync(sumula.JogoId, cancellationToken)
                ?? throw new DomainException("Jogo da súmula não encontrado");

            sumula.Encerrar();
            jogo.EncerrarPelaSumula(sumula.PlacarCasa, sumula.PlacarVisitante);
            await _avanco.ExecutarAsync(jogo.TemporadaId, cancellationToken);
            await _sumulaRepository.UnitOfWork.Commit();

            await _hub.Clients.Group($"partida-{sumula.Id}")
                .SendAsync("SumulaEncerrada", new { PartidaId = sumula.Id, sumula.PlacarCasa, sumula.PlacarVisitante }, cancellationToken);
            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
