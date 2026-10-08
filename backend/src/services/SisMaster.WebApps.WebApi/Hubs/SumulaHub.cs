using Microsoft.AspNetCore.SignalR;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Hubs.Dtos;

namespace SisMaster.WebApps.WebApi.Hubs;

public class SumulaHub : Hub
{
    private readonly ISumulaRepository _partidaRepository;

    public SumulaHub(ISumulaRepository partidaRepository)
    {
        _partidaRepository = partidaRepository;
    }

    public override async Task OnConnectedAsync()
    {
        var partidaIdStr = Context.GetHttpContext()?.Request.Query["partidaId"].ToString();
        if (!string.IsNullOrEmpty(partidaIdStr) && Guid.TryParse(partidaIdStr, out var partidaId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"partida-{partidaId}");

            // Envia estado atual para o cliente recém-conectado
            var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
            if (partida is not null)
                await Clients.Caller.SendAsync("EstadoAtual", MapEstadoDto(partida));
        }

        await base.OnConnectedAsync();
    }

    public async Task EntrarComoMesarioTempo(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"mesario-tempo-{partidaId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"partida-{partidaId}");

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is not null)
            await Clients.Caller.SendAsync("EstadoAtual", MapEstadoDto(partida));
    }

    public async Task EntrarComoMesarioStats(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"mesario-stats-{partidaId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"partida-{partidaId}");

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is not null)
            await Clients.Caller.SendAsync("EstadoAtual", MapEstadoDto(partida));
    }

    public async Task IniciarCronometro(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is null) return;

        partida.IniciarCronometro();
        _partidaRepository.Atualizar(partida);
        await _partidaRepository.UnitOfWork.Commit();

        await Clients.Group($"partida-{partidaId}").SendAsync("CronometroAtualizado", MapCronometroDto(partida));
    }

    public async Task PausarCronometro(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is null) return;

        partida.PausarCronometro();
        _partidaRepository.Atualizar(partida);
        await _partidaRepository.UnitOfWork.Commit();

        await Clients.Group($"partida-{partidaId}").SendAsync("CronometroAtualizado", MapCronometroDto(partida));
    }

    public async Task ResetarShotClock(string partidaIdStr, int segundos = 24)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is null) return;

        partida.ResetarShotClock(segundos);
        _partidaRepository.Atualizar(partida);
        await _partidaRepository.UnitOfWork.Commit();

        await Clients.Group($"partida-{partidaId}").SendAsync("CronometroAtualizado", MapCronometroDto(partida));
    }

    public async Task AlternarPosse(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is null) return;

        partida.AlternarPosse();
        _partidaRepository.Atualizar(partida);
        await _partidaRepository.UnitOfWork.Commit();

        await Clients.Group($"partida-{partidaId}").SendAsync("CronometroAtualizado", MapCronometroDto(partida));
    }

    public async Task AvancarPeriodo(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is null) return;

        partida.AvancarPeriodo();
        _partidaRepository.Atualizar(partida);
        await _partidaRepository.UnitOfWork.Commit();

        await Clients.Group($"partida-{partidaId}").SendAsync("CronometroAtualizado", MapCronometroDto(partida));
    }

    /// <summary>Quem está em quadra em cada time (ids dos jogadores da súmula).</summary>
    public static object MapQuadra(Sumula partida) => new
    {
        PartidaId = partida.Id,
        Casa = partida.QuadraDo(partida.TimeDoLado(LadoTime.Casa)),
        Visitante = partida.QuadraDo(partida.TimeDoLado(LadoTime.Visitante))
    };

    private static CronometroDto MapCronometroDto(Sumula partida) => new()
    {
        PartidaId = partida.Id,
        Ativo = partida.CronometroAtivo,
        SegundosRestantes = partida.CronometroSegundosRestantes,
        IniciadoEmMs = partida.CronometroIniciadoEm.HasValue
            ? new DateTimeOffset(partida.CronometroIniciadoEm.Value).ToUnixTimeMilliseconds()
            : null,
        SegundosAoIniciar = partida.CronometroSegundosAoIniciar,
        ShotClockAtivo = partida.ShotClockAtivo,
        ShotClockSegundos = partida.ShotClockSegundosRestantes,
        ShotClockIniciadoEmMs = partida.ShotClockIniciadoEm.HasValue
            ? new DateTimeOffset(partida.ShotClockIniciadoEm.Value).ToUnixTimeMilliseconds()
            : null,
        ShotClockSegundosAoIniciar = partida.ShotClockSegundosAoIniciar,
        PosseCasa = partida.PosseCasa,
        PeriodoAtual = (int)partida.PeriodoAtual
    };

    private static EstadoPartidaDto MapEstadoDto(Sumula partida)
    {
        var ultimo = partida.Eventos.LastOrDefault();
        return new EstadoPartidaDto
        {
            PartidaId = partida.Id,
            Status = partida.Status.ToString(),
            PeriodoAtual = (int)partida.PeriodoAtual,
            PlacarCasa = partida.PlacarCasa,
            PlacarVisitante = partida.PlacarVisitante,
            FaltasCasa = partida.FaltasCasa,
            FaltasVisitante = partida.FaltasVisitante,
            Cronometro = MapCronometroDto(partida),
            UltimoEvento = ultimo is null ? null : new UltimoEventoDto
            {
                EventoId = ultimo.Id,
                JogadorId = ultimo.JogadorId,
                Tipo = ultimo.Tipo.ToString(),
                PeriodoAtual = (int)ultimo.Periodo,
                TempoJogoSegundos = ultimo.TempoJogoSegundos
            }
        };
    }
}
