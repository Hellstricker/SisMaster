using FluentValidation.Results;
using SisMaster.Core.Communications;
using Microsoft.AspNetCore.SignalR;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Auditoria;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;
using SisMaster.WebApps.WebApi.Hubs;

namespace SisMaster.WebApps.WebApi.Application.Commands.Handlers;

public class ReabrirSumulaCommandHandler : IRequestHandler<ReabrirSumulaCommand, ValidationResult>
{
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IJogoRepository _jogoRepository;
    private readonly IHubContext<SumulaHub> _hub;
    private readonly DesfazerWOServico _regras;
    private readonly AvancoDaTemporada _avanco;
    private readonly IAuditoria _auditoria;

    public ReabrirSumulaCommandHandler(ISumulaRepository sumulaRepository, IJogoRepository jogoRepository, IHubContext<SumulaHub> hub,
        DesfazerWOServico regras, AvancoDaTemporada avanco, IAuditoria auditoria)
    {
        _sumulaRepository = sumulaRepository;
        _jogoRepository = jogoRepository;
        _hub = hub;
        _regras = regras;
        _avanco = avanco;
        _auditoria = auditoria;
    }

    public async Task<ValidationResult> Handle(ReabrirSumulaCommand request, CancellationToken cancellationToken)
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

            // Súmula importada ou não encerrada: a própria súmula recusa. Depois, o que depende do resultado nas fases seguintes.
            if (sumula.Status != StatusSumula.Encerrada) throw new DomainException("A súmula não está encerrada");
            if (sumula.CodigoExterno is not null) throw new DomainException("Súmula importada do FIBA LiveStats não pode ser reaberta: importe o jogo de novo");
            var impedimento = await _regras.MotivoQueImpedeReabrirAsync(jogo, cancellationToken);
            if (impedimento is not null) throw new DomainException(impedimento);

            var placar = $"{jogo.PlacarCasa} × {jogo.PlacarVisitante}";
            sumula.Reabrir();
            jogo.ReabrirPelaSumula();

            // Se o resultado decidia uma série, o jogo "se necessário" dispensado volta a Agendado; o avanço dispensa de novo
            // o que ainda estiver decidido pelos outros resultados.
            if (jogo.ConfrontoId is { } confronto)
            {
                var dados = await _jogoRepository.CarregarParaAvancoAsync(jogo.TemporadaId, cancellationToken);
                foreach (var outro in dados.Jogos.Where(j => j.ConfrontoId == confronto && j.Status == StatusJogo.Dispensado))
                    outro.ReativarDispensado();
            }
            await _avanco.ExecutarAsync(jogo.TemporadaId, cancellationToken);

            _auditoria.Registrar("ReabrirSumula", "Jogo", jogo.Id, $"Súmula reaberta (resultado {placar} retirado). Motivo: {request.Motivo.Trim()}");
            await _sumulaRepository.UnitOfWork.Commit();

            // A TV e as mesas voltam ao jogo em andamento.
            await _hub.Clients.Group($"partida-{sumula.Id}").SendAsync("EstadoAtual", SumulaHub.MapEstadoDto(sumula), cancellationToken);
            return request.ValidationResult;
        }
        catch (DomainException ex)
        {
            request.ValidationResult.Errors.Add(new ValidationFailure(string.Empty, ex.Message));
            return request.ValidationResult;
        }
    }
}
