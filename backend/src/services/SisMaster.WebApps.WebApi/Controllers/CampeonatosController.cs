using Microsoft.AspNetCore.Mvc;
using SisMaster.Core.Communications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

[Route("api/campeonatos")]
public class CampeonatosController : MainController
{
    private readonly IMediatorHandler _mediator;
    private readonly ICampeonatoRepository _repository;

    public CampeonatosController(IMediatorHandler mediator, ICampeonatoRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var c = await _repository.ObterPorIdAsync(id, ct);
        if (c is null) return NotFound();
        return CustomResponse(new
        {
            c.Id,
            c.Nome,
            c.AssociacaoId,
            Temporadas = c.Temporadas
                .OrderByDescending(t => t.Ano)
                .Select(t => new
                {
                    t.Id,
                    t.Ano,
                    t.Status,
                    DataInicioInscricoes = t.DataInicioInscricoes.ToString("yyyy-MM-dd"),
                    DataFimInscricoes = t.DataFimInscricoes.ToString("yyyy-MM-dd"),
                    t.InscricoesHabilitadas,
                    t.ValorBonificacaoPorAtleta
                })
        });
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult> Editar(Guid id, [FromBody] EditarCampeonatoCommand command)
    {
        command.CampeonatoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPost("{id:guid}/temporadas")]
    public async Task<ActionResult> CriarTemporada(Guid id, [FromBody] CriarTemporadaCommand command, CancellationToken ct)
    {
        command.CampeonatoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("{campeonatoId:guid}/temporadas/{temporadaId:guid}/encerrar-inscricoes")]
    public async Task<ActionResult> EncerrarInscricoes(Guid campeonatoId, Guid temporadaId, CancellationToken ct)
    {
        var command = new EncerrarInscricoesCommand { CampeonatoId = campeonatoId, TemporadaId = temporadaId };
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("{campeonatoId:guid}/temporadas/{temporadaId:guid}/inscricoes-habilitadas")]
    public async Task<ActionResult> AlterarInscricoesHabilitadas(Guid campeonatoId, Guid temporadaId, [FromBody] AlterarInscricoesHabilitadasCommand command, CancellationToken ct)
    {
        command.CampeonatoId = campeonatoId;
        command.TemporadaId = temporadaId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("{campeonatoId:guid}/temporadas/{temporadaId:guid}/bonificacao")]
    public async Task<ActionResult> ConfigurarBonificacao(Guid campeonatoId, Guid temporadaId, [FromBody] ConfigurarBonificacaoCommand command, CancellationToken ct)
    {
        command.CampeonatoId = campeonatoId;
        command.TemporadaId = temporadaId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("{campeonatoId:guid}/temporadas/{temporadaId:guid}/iniciar")]
    public async Task<ActionResult> IniciarTemporada(Guid campeonatoId, Guid temporadaId, CancellationToken ct)
    {
        var command = new IniciarTemporadaCommand { CampeonatoId = campeonatoId, TemporadaId = temporadaId };
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("{campeonatoId:guid}/temporadas/{temporadaId:guid}/encerrar")]
    public async Task<ActionResult> EncerrarTemporada(Guid campeonatoId, Guid temporadaId, CancellationToken ct)
    {
        var command = new EncerrarTemporadaCommand { CampeonatoId = campeonatoId, TemporadaId = temporadaId };
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }
}
