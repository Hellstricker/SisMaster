using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SisMaster.Core.Communications;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Application.Importacao.Fiba;
using SisMaster.WebApps.WebApi.Application.Queries;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Hubs;
using SisMaster.WebApps.WebApi.Hubs.Dtos;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

// "/api/partidas" é o contrato que as telas estáticas do mesário e do placar usam; "/api/sumulas" é o nome novo.
[Route("api/partidas")]
[Route("api/sumulas")]
public class SumulasController : MainController
{
    private readonly IMediatorHandler _mediator;
    private readonly ISumulaRepository _partidaRepository;
    private readonly IHubContext<SumulaHub> _hub;
    private readonly RodizioDaSumulaQuery _rodizioQuery;

    public SumulasController(
        IMediatorHandler mediator,
        ISumulaRepository partidaRepository,
        IHubContext<SumulaHub> hub,
        RodizioDaSumulaQuery rodizioQuery)
    {
        _rodizioQuery = rodizioQuery;
        _mediator = mediator;
        _partidaRepository = partidaRepository;
        _hub = hub;
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

        var timeCasa = partida.TimeDoLado(LadoTime.Casa);
        var timeVisitante = partida.TimeDoLado(LadoTime.Visitante);

        var estatisticas = CalcularEstatisticas(partida);

        return CustomResponse(new
        {
            partida.Id,
            Status = partida.Status.ToString(),
            Periodo = (int)partida.PeriodoAtual,
            partida.PlacarCasa,
            partida.PlacarVisitante,
            TimeCasa = new { timeCasa.Id, timeCasa.Nome, Sigla = Sigla(timeCasa.Nome) },
            TimeVisitante = new { timeVisitante.Id, timeVisitante.Nome, Sigla = Sigla(timeVisitante.Nome) },
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

    [HttpPost("{id:guid}/encerrar")]
    public async Task<ActionResult> Encerrar(Guid id)
    {
        var result = await _mediator.EnviarComando(new EncerrarSumulaCommand { SumulaId = id });
        return CustomResponse(result);
    }

    [HttpPost("{id:guid}/eventos")]
    public async Task<ActionResult> RegistrarEvento(Guid id, [FromBody] RegistrarEventoCommand command, CancellationToken ct)
    {
        command.PartidaId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    /// <summary>Quadra, banco e rodízio de cada time (mesário de estatísticas), derivados das substituições.</summary>
    [HttpGet("{id:guid}/rodizio")]
    public async Task<ActionResult> Rodizio(Guid id, CancellationToken ct)
    {
        var resultado = await _rodizioQuery.ExecutarAsync(id, ct);
        return resultado is null ? NotFound() : CustomResponse(resultado);
    }

    /// <summary>Prévia da importação das estatísticas do FIBA LiveStats nesta súmula (não grava nada).</summary>
    [HttpPost("{id:guid}/importacao-fiba/previa")]
    public async Task<ActionResult> PreviaImportacaoFiba(
        Guid id, [FromBody] PreviaImportacaoFibaRequest request, [FromServices] ImportacaoFibaPreviaQuery query, CancellationToken ct)
    {
        try
        {
            var previa = await query.ExecutarAsync(id, request.Codigo, request.InverterLados, ct);
            return previa is null ? NotFound() : CustomResponse(previa);
        }
        catch (ArgumentException ex)
        {
            AdicionarErro(ex.Message);
        }
        catch (HttpRequestException)
        {
            AdicionarErro("Não foi possível obter o jogo no FIBA LiveStats — confira o código e tente de novo");
        }
        return CustomResponse();
    }

    public sealed record PreviaImportacaoFibaRequest(string? Codigo, bool InverterLados = false);

    /// <summary>Substitui os dados da súmula pelos do jogo importado do FIBA LiveStats e encerra o jogo (só se a conferência fechar).</summary>
    [HttpPost("{id:guid}/importacao-fiba")]
    public async Task<ActionResult> ImportarFiba(Guid id, [FromBody] ImportarFibaCommand command)
    {
        command.SumulaId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    /// <summary>Acrescenta um atleta do elenco à súmula depois do início do jogo (chegou atrasado).</summary>
    [HttpPost("{id:guid}/relacionados")]
    public async Task<ActionResult> AcrescentarAtleta(Guid id, [FromBody] AcrescentarAtletaNaSumulaCommand command)
    {
        command.SumulaId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPost("{id:guid}/substituicoes")]
    public async Task<ActionResult> RegistrarSubstituicao(Guid id, [FromBody] RegistrarSubstituicaoCommand command)
    {
        command.PartidaId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpDelete("{id:guid}/substituicoes/ultima")]
    public async Task<ActionResult> DesfazerUltimaSubstituicao(Guid id)
    {
        var result = await _mediator.EnviarComando(new DesfazerUltimaSubstituicaoCommand { PartidaId = id });
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
            partida.DesfazerUltimoEvento();
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

        object Lado(Time t) => new
        {
            t.Id,
            t.Nome,
            Sigla = Sigla(t.Nome),
            t.Cor,
            t.Tecnico,
            t.AuxiliarTecnico,
            t.CapitaoId,
            Jogadores = t.Jogadores
                .OrderBy(j => int.TryParse(j.Numero, out var n) ? n : 0).ThenBy(j => j.Numero)
                .Select(j => new { j.Id, j.Numero, j.Nome, j.Titular })
        };

        return CustomResponse(new
        {
            TimeCasa = Lado(partida.TimeDoLado(LadoTime.Casa)),
            TimeVisitante = Lado(partida.TimeDoLado(LadoTime.Visitante))
        });
    }

    /// <summary>Sigla de exibição para o placar (as três primeiras letras do nome).</summary>
    private static string Sigla(string nome) => new string(nome.Where(char.IsLetterOrDigit).Take(3).ToArray()).ToUpperInvariant();

    private static Dictionary<Guid, object> CalcularEstatisticas(Sumula partida)
    {
        var stats = new Dictionary<Guid, (int pts, int reb, int ast, int stl, int blk, int fouls)>();

        foreach (var ev in partida.Eventos)
        {
            if (!stats.ContainsKey(ev.JogadorId))
                stats[ev.JogadorId] = (0, 0, 0, 0, 0, 0);

            var s = stats[ev.JogadorId];
            stats[ev.JogadorId] = ev.Tipo switch
            {
                var t when t is Domain.Sumula.Enums.TipoEvento.Ponto2
                        or Domain.Sumula.Enums.TipoEvento.Ponto3
                        or Domain.Sumula.Enums.TipoEvento.LanceLivre
                    => (s.pts + ev.PontosGerados, s.reb, s.ast, s.stl, s.blk, s.fouls),

                var t when t is Domain.Sumula.Enums.TipoEvento.ReboteOfensivo
                        or Domain.Sumula.Enums.TipoEvento.ReboteDefensivo
                    => (s.pts, s.reb + 1, s.ast, s.stl, s.blk, s.fouls),

                Domain.Sumula.Enums.TipoEvento.Assistencia
                    => (s.pts, s.reb, s.ast + 1, s.stl, s.blk, s.fouls),

                Domain.Sumula.Enums.TipoEvento.Roubada
                    => (s.pts, s.reb, s.ast, s.stl + 1, s.blk, s.fouls),

                Domain.Sumula.Enums.TipoEvento.Toco
                    => (s.pts, s.reb, s.ast, s.stl, s.blk + 1, s.fouls),

                var t when t is Domain.Sumula.Enums.TipoEvento.FaltaPessoal
                        or Domain.Sumula.Enums.TipoEvento.FaltaTecnica
                        or Domain.Sumula.Enums.TipoEvento.FaltaAntiDesportiva
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
