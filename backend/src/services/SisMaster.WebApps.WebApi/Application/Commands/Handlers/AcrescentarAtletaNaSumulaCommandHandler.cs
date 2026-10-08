using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Hubs;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class AcrescentarAtletaNaSumulaCommandHandler : IRequestHandler<AcrescentarAtletaNaSumulaCommand, ValidationResult>
{
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly IHubContext<SumulaHub> _hub;

    public AcrescentarAtletaNaSumulaCommandHandler(ISumulaRepository sumulaRepository, IEquipeRepository equipeRepository, IHubContext<SumulaHub> hub)
    {
        _sumulaRepository = sumulaRepository;
        _equipeRepository = equipeRepository;
        _hub = hub;
    }

    public async Task<ValidationResult> Handle(AcrescentarAtletaNaSumulaCommand request, CancellationToken cancellationToken)
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

            var time = sumula.TimeDoLado(request.Lado);
            var elenco = await _equipeRepository.ObterElencoAsync(time.EquipeId, cancellationToken);
            var atleta = elenco.FirstOrDefault(a => a.AtletaId == request.AtletaId)
                ?? throw new DomainException("O atleta não pertence ao elenco desta equipe");

            var jogador = sumula.AcrescentarJogador(request.Lado, new RelacionadoNovo(atleta.AtletaId, atleta.Nome, request.Numero.Trim(), false));
            _sumulaRepository.AdicionarJogadores([jogador]);
            await _sumulaRepository.UnitOfWork.Commit();

            await _hub.Clients.Group($"partida-{sumula.Id}").SendAsync("RelacaoAtualizada", new { PartidaId = sumula.Id }, cancellationToken);
            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
