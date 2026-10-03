using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SisMaster.Core.Communications;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Domain.Partida;
using SisMaster.WebApps.WebApi.Domain.Time;
using SisMaster.WebApps.WebApi.Hubs;
using SisMaster.WebApps.WebApi.Hubs.Dtos;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

[Route("api/partidas")]
public class PartidasController : MainController
{
    private readonly IMediatorHandler _mediator;
    private readonly IPartidaRepository _partidaRepository;
    private readonly ITimeRepository _timeRepository;
    private readonly IHubContext<SumulaHub> _hub;

    public PartidasController(
        IMediatorHandler mediator,
        IPartidaRepository partidaRepository,
        ITimeRepository timeRepository,
        IHubContext<SumulaHub> hub)
    {
        _mediator = mediator;
        _partidaRepository = partidaRepository;
        _timeRepository = timeRepository;
        _hub = hub;
    }

    [HttpPost]
    public async Task<ActionResult> Criar([FromBody] CriarPartidaCommand command, CancellationToken ct)
    {
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpGet("{id:guid}/placar")]
    public async Task<ActionResult> ObterPlacar(Guid id, CancellationToken ct)
    {
        var partida = await _partidaRepository.ObterPorIdAsync(id, ct);
        if (partida is null) return NotFound();

        return CustomResponse(new
        {
            partida.Id,
            Status = partida.Status.ToString(),
            Periodo = (int)partida.PeriodoAtual,
            partida.PlacarCasa,
            partida.PlacarVisitante,
            partida.FaltasCasa,
            partida.FaltasVisitante,
            Cronometro = new
            {
                partida.CronometroAtivo,
                partida.CronometroSegundosRestantes,
                IniciadoEmMs = partida.CronometroIniciadoEm.HasValue
                    ? new DateTimeOffset(partida.CronometroIniciadoEm.Value).ToUnixTimeMilliseconds()
                    : (long?)null,
                partida.CronometroSegundosAoIniciar
            },
            ShotClock = new
            {
                partida.ShotClockAtivo,
                partida.ShotClockSegundosRestantes,
                IniciadoEmMs = partida.ShotClockIniciadoEm.HasValue
                    ? new DateTimeOffset(partida.ShotClockIniciadoEm.Value).ToUnixTimeMilliseconds()
                    : (long?)null,
                partida.ShotClockSegundosAoIniciar
            },
            partida.PosseCasa
        });
    }

    [HttpGet("{id:guid}/sumula")]
    public async Task<ActionResult> ObterSumula(Guid id, CancellationToken ct)
    {
        var partida = await _partidaRepository.ObterPorIdAsync(id, ct);
        if (partida is null) return NotFound();

        var timeCasa = await _timeRepository.ObterPorIdAsync(partida.TimeCasaId, ct);
        var timeVisitante = await _timeRepository.ObterPorIdAsync(partida.TimeVisitanteId, ct);

        var estatisticas = CalcularEstatisticas(partida);

        return CustomResponse(new
        {
            partida.Id,
            Status = partida.Status.ToString(),
            Periodo = (int)partida.PeriodoAtual,
            partida.PlacarCasa,
            partida.PlacarVisitante,
            TimeCasa = timeCasa is null ? null : new { timeCasa.Id, timeCasa.Nome, timeCasa.Sigla },
            TimeVisitante = timeVisitante is null ? null : new { timeVisitante.Id, timeVisitante.Nome, timeVisitante.Sigla },
            Eventos = partida.Eventos.OrderByDescending(e => e.RegistradoEm).Select(e => new
            {
                e.Id,
                e.JogadorId,
                Tipo = e.Tipo.ToString(),
                Periodo = (int)e.Periodo,
                e.TempoJogoSegundos,
                e.RegistradoEm
            }),
            Estatisticas = estatisticas
        });
    }

    [HttpPost("{id:guid}/eventos")]
    public async Task<ActionResult> RegistrarEvento(Guid id, [FromBody] RegistrarEventoCommand command, CancellationToken ct)
    {
        command.PartidaId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpDelete("{id:guid}/eventos/ultimo")]
    public async Task<ActionResult> DesfazerUltimoEvento(Guid id, CancellationToken ct)
    {
        var partida = await _partidaRepository.ObterPorIdAsync(id, ct);
        if (partida is null) return NotFound();

        if (!partida.Eventos.Any())
        {
            AdicionarErro("Não há eventos para desfazer");
            return CustomResponse();
        }

        try
        {
            var ultimo = partida.Eventos.Last();
            var jogador = await _timeRepository.ObterJogadorPorIdAsync(ultimo.JogadorId, ct);
            if (jogador is null)
            {
                AdicionarErro("Jogador do último evento não encontrado");
                return CustomResponse();
            }

            partida.DesfazerUltimoEvento(jogador.TimeId);
            _partidaRepository.Atualizar(partida);
            await _partidaRepository.UnitOfWork.Commit();

            // Broadcast do estado atualizado para todos na partida
            var estado = new EstadoPartidaDto
            {
                PartidaId = partida.Id,
                Status = partida.Status.ToString(),
                PeriodoAtual = (int)partida.PeriodoAtual,
                PlacarCasa = partida.PlacarCasa,
                PlacarVisitante = partida.PlacarVisitante,
                FaltasCasa = partida.FaltasCasa,
                FaltasVisitante = partida.FaltasVisitante,
                Cronometro = new CronometroDto
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
                },
                UltimoEvento = partida.Eventos.LastOrDefault() is { } novoUltimo
                    ? new UltimoEventoDto
                    {
                        EventoId = novoUltimo.Id,
                        JogadorId = novoUltimo.JogadorId,
                        Tipo = novoUltimo.Tipo.ToString(),
                        PeriodoAtual = (int)novoUltimo.Periodo,
                        TempoJogoSegundos = novoUltimo.TempoJogoSegundos
                    }
                    : null
            };

            await _hub.Clients.Group($"partida-{id}").SendAsync("EstadoAtual", estado, ct);

            return CustomResponse(new { Mensagem = "Evento desfeito com sucesso" });
        }
        catch (DomainException ex)
        {
            AdicionarErro(ex.Message);
            return CustomResponse();
        }
    }

    [HttpGet("{id:guid}/jogadores")]
    public async Task<ActionResult> ListarJogadoresDaPartida(Guid id, CancellationToken ct)
    {
        var partida = await _partidaRepository.ObterPorIdAsync(id, ct);
        if (partida is null) return NotFound();

        var timeCasa = await _timeRepository.ObterPorIdAsync(partida.TimeCasaId, ct);
        var timeVisitante = await _timeRepository.ObterPorIdAsync(partida.TimeVisitanteId, ct);

        return CustomResponse(new
        {
            TimeCasa = timeCasa is null ? null : new
            {
                timeCasa.Id,
                timeCasa.Nome,
                timeCasa.Sigla,
                Jogadores = timeCasa.Jogadores
                    .Where(j => j.Ativo)
                    .OrderBy(j => j.Numero)
                    .Select(j => new { j.Id, j.Numero, j.Pessoa.Nome })
            },
            TimeVisitante = timeVisitante is null ? null : new
            {
                timeVisitante.Id,
                timeVisitante.Nome,
                timeVisitante.Sigla,
                Jogadores = timeVisitante.Jogadores
                    .Where(j => j.Ativo)
                    .OrderBy(j => j.Numero)
                    .Select(j => new { j.Id, j.Numero, j.Pessoa.Nome })
            }
        });
    }

    private static Dictionary<Guid, object> CalcularEstatisticas(Partida partida)
    {
        var stats = new Dictionary<Guid, (int pts, int reb, int ast, int stl, int blk, int fouls)>();

        foreach (var ev in partida.Eventos)
        {
            if (!stats.ContainsKey(ev.JogadorId))
                stats[ev.JogadorId] = (0, 0, 0, 0, 0, 0);

            var s = stats[ev.JogadorId];
            stats[ev.JogadorId] = ev.Tipo switch
            {
                var t when t is Domain.Partida.Enums.TipoEvento.Ponto2
                        or Domain.Partida.Enums.TipoEvento.Ponto3
                        or Domain.Partida.Enums.TipoEvento.LanceLivre
                    => (s.pts + ev.PontosGerados, s.reb, s.ast, s.stl, s.blk, s.fouls),

                var t when t is Domain.Partida.Enums.TipoEvento.ReboteOfensivo
                        or Domain.Partida.Enums.TipoEvento.ReboteDefensivo
                    => (s.pts, s.reb + 1, s.ast, s.stl, s.blk, s.fouls),

                Domain.Partida.Enums.TipoEvento.Assistencia
                    => (s.pts, s.reb, s.ast + 1, s.stl, s.blk, s.fouls),

                Domain.Partida.Enums.TipoEvento.Roubada
                    => (s.pts, s.reb, s.ast, s.stl + 1, s.blk, s.fouls),

                Domain.Partida.Enums.TipoEvento.Toco
                    => (s.pts, s.reb, s.ast, s.stl, s.blk + 1, s.fouls),

                var t when t is Domain.Partida.Enums.TipoEvento.FaltaPessoal
                        or Domain.Partida.Enums.TipoEvento.FaltaTecnica
                        or Domain.Partida.Enums.TipoEvento.FaltaAntiDesportiva
                    => (s.pts, s.reb, s.ast, s.stl, s.blk, s.fouls + 1),

                _ => s
            };
        }

        return stats.ToDictionary(
            kvp => kvp.Key,
            kvp => (object)new
            {
                JogadorId = kvp.Key,
                Pontos = kvp.Value.pts,
                Rebotes = kvp.Value.reb,
                Assistencias = kvp.Value.ast,
                Roubadas = kvp.Value.stl,
                Tocos = kvp.Value.blk,
                Faltas = kvp.Value.fouls
            });
    }
}
