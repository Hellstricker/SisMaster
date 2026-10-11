using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApi.Core.Usuarios;
using SisMaster.WebApps.WebApi.Seguranca;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Hubs.Dtos;

namespace SisMaster.WebApps.WebApi.Hubs;

public class SumulaHub : Hub
{
    private readonly ISumulaRepository _partidaRepository;
    private readonly IResolvedorDeAssociacao _resolvedor;

    public SumulaHub(ISumulaRepository partidaRepository, IResolvedorDeAssociacao resolvedor)
    {
        _partidaRepository = partidaRepository;
        _resolvedor = resolvedor;
    }

    /// <summary>
    /// Ver o placar é público (OnConnectedAsync é anônimo). Operar a mesa exige perfil de árbitro (ou superior) E que o jogo
    /// seja de uma associação do usuário; senão a chamada é recusada.
    /// </summary>
    private async Task GarantirAcessoAsync(Guid partidaId)
    {
        var acesso = new AcessoDoUsuario(Context.User);
        if (acesso.EhAdministrador) return;
        var associacao = await _resolvedor.AssociacaoDoAsync(Recurso.Sumula, partidaId);
        if (associacao is null || !acesso.PodeAcessar(associacao.Value))
            throw new HubException("Sem permissão para operar esta partida.");
    }

    [AllowAnonymous]
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

    [Authorize(Policy = Politicas.Arbitragem)]
    public async Task EntrarComoMesarioTempo(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;
        await GarantirAcessoAsync(partidaId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"mesario-tempo-{partidaId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"partida-{partidaId}");

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is not null)
            await Clients.Caller.SendAsync("EstadoAtual", MapEstadoDto(partida));
    }

    [Authorize(Policy = Politicas.Arbitragem)]
    public async Task EntrarComoMesarioStats(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;
        await GarantirAcessoAsync(partidaId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"mesario-stats-{partidaId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"partida-{partidaId}");

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is not null)
            await Clients.Caller.SendAsync("EstadoAtual", MapEstadoDto(partida));
    }

    [Authorize(Policy = Politicas.Arbitragem)]
    public async Task IniciarCronometro(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;
        await GarantirAcessoAsync(partidaId);

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is null) return;

        partida.IniciarCronometro();
        _partidaRepository.Atualizar(partida);
        await _partidaRepository.UnitOfWork.Commit();

        await Clients.Group($"partida-{partidaId}").SendAsync("CronometroAtualizado", MapCronometroDto(partida));
    }

    [Authorize(Policy = Politicas.Arbitragem)]
    public async Task PausarCronometro(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;
        await GarantirAcessoAsync(partidaId);

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is null) return;

        partida.PausarCronometro();
        _partidaRepository.Atualizar(partida);
        await _partidaRepository.UnitOfWork.Commit();

        await Clients.Group($"partida-{partidaId}").SendAsync("CronometroAtualizado", MapCronometroDto(partida));
    }

    [Authorize(Policy = Politicas.Arbitragem)]
    public async Task ResetarShotClock(string partidaIdStr, int segundos = 24)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;
        await GarantirAcessoAsync(partidaId);

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is null) return;

        partida.ResetarShotClock(segundos);
        _partidaRepository.Atualizar(partida);
        await _partidaRepository.UnitOfWork.Commit();

        await Clients.Group($"partida-{partidaId}").SendAsync("CronometroAtualizado", MapCronometroDto(partida));
    }

    [Authorize(Policy = Politicas.Arbitragem)]
    public async Task AlternarPosse(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;
        await GarantirAcessoAsync(partidaId);

        var partida = await _partidaRepository.ObterPorIdAsync(partidaId);
        if (partida is null) return;

        partida.AlternarPosse();
        _partidaRepository.Atualizar(partida);
        await _partidaRepository.UnitOfWork.Commit();

        await Clients.Group($"partida-{partidaId}").SendAsync("CronometroAtualizado", MapCronometroDto(partida));
    }

    [Authorize(Policy = Politicas.Arbitragem)]
    public async Task AvancarPeriodo(string partidaIdStr)
    {
        if (!Guid.TryParse(partidaIdStr, out var partidaId)) return;
        await GarantirAcessoAsync(partidaId);

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

    public static CronometroDto MapCronometroDto(Sumula partida) => new()
    {
        PartidaId = partida.Id,
        Ativo = partida.CronometroAtivo,
        SegundosRestantes = partida.CronometroSegundosRestantes,
        IniciadoEmMs = EmMs(partida.CronometroIniciadoEm),
        SegundosAoIniciar = partida.CronometroSegundosAoIniciar,
        ShotClockAtivo = partida.ShotClockAtivo,
        ShotClockSegundos = partida.ShotClockSegundosRestantes,
        ShotClockIniciadoEmMs = EmMs(partida.ShotClockIniciadoEm),
        ShotClockSegundosAoIniciar = partida.ShotClockSegundosAoIniciar,
        PosseCasa = partida.PosseCasa,
        PeriodoAtual = (int)partida.PeriodoAtual,
        FaltasCasa = partida.FaltasDeEquipeParaExibir(LadoTime.Casa),
        FaltasVisitante = partida.FaltasDeEquipeParaExibir(LadoTime.Visitante),
        AgoraMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };

    /// <summary>Os instantes são gravados em UTC; o banco os devolve sem "tipo", então o UTC é dito aqui (senão dependeria do fuso do servidor).</summary>
    private static long? EmMs(DateTime? instante) =>
        instante.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(instante.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds() : null;

    public static EstadoPartidaDto MapEstadoDto(Sumula partida)
    {
        var ultimo = partida.Eventos.LastOrDefault();
        return new EstadoPartidaDto
        {
            PartidaId = partida.Id,
            Status = partida.Status.ToString(),
            PeriodoAtual = (int)partida.PeriodoAtual,
            PlacarCasa = partida.PlacarCasa,
            PlacarVisitante = partida.PlacarVisitante,
            FaltasCasa = partida.FaltasDeEquipeParaExibir(LadoTime.Casa),
            FaltasVisitante = partida.FaltasDeEquipeParaExibir(LadoTime.Visitante),
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
