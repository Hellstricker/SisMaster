using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Hubs;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class RegistrarSubstituicaoCommandHandler : IRequestHandler<RegistrarSubstituicaoCommand, ValidationResult>
{
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IHubContext<SumulaHub> _hub;

    public RegistrarSubstituicaoCommandHandler(ISumulaRepository sumulaRepository, IHubContext<SumulaHub> hub)
    {
        _sumulaRepository = sumulaRepository;
        _hub = hub;
    }

    public async Task<ValidationResult> Handle(RegistrarSubstituicaoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        var sumula = await _sumulaRepository.ObterPorIdAsync(request.PartidaId, cancellationToken);
        if (sumula is null)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure("PartidaId", "Súmula não encontrada"));
            return request.ValidationResult;
        }

        try
        {
            var substituicao = sumula.RegistrarSubstituicao(request.JogadorSaiId, request.JogadorEntraId, request.TempoJogoSegundos);
            _sumulaRepository.AdicionarSubstituicao(substituicao);
            await _sumulaRepository.UnitOfWork.Commit();

            await _hub.Clients.Group($"partida-{sumula.Id}")
                .SendAsync("QuadraAtualizada", SumulaHub.MapQuadra(sumula), cancellationToken);
            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}

public class DesfazerUltimaSubstituicaoCommandHandler : IRequestHandler<DesfazerUltimaSubstituicaoCommand, ValidationResult>
{
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IHubContext<SumulaHub> _hub;

    public DesfazerUltimaSubstituicaoCommandHandler(ISumulaRepository sumulaRepository, IHubContext<SumulaHub> hub)
    {
        _sumulaRepository = sumulaRepository;
        _hub = hub;
    }

    public async Task<ValidationResult> Handle(DesfazerUltimaSubstituicaoCommand request, CancellationToken cancellationToken)
    {
        if (!request.EhValido()) return request.ValidationResult;

        var sumula = await _sumulaRepository.ObterPorIdAsync(request.PartidaId, cancellationToken);
        if (sumula is null)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure("PartidaId", "Súmula não encontrada"));
            return request.ValidationResult;
        }

        try
        {
            var removida = sumula.DesfazerUltimaSubstituicao();
            _sumulaRepository.RemoverSubstituicao(removida);
            await _sumulaRepository.UnitOfWork.Commit();

            await _hub.Clients.Group($"partida-{sumula.Id}")
                .SendAsync("QuadraAtualizada", SumulaHub.MapQuadra(sumula), cancellationToken);
            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
