using Microsoft.AspNetCore.Mvc;
using SisMaster.Core.Communications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

[Route("api/temporadas")]
public class TemporadasController : MainController
{
    private readonly IMediatorHandler _mediator;
    private readonly ITemporadaRepository _repository;

    public TemporadasController(IMediatorHandler mediator, ITemporadaRepository repository)
    {
        _mediator = mediator;
        _repository = repository;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var t = await _repository.ObterPorIdAsync(id, ct);
        if (t is null) return NotFound();
        return CustomResponse(new
        {
            t.Id,
            t.CampeonatoId,
            AssociacaoId = t.Campeonato.AssociacaoId,
            t.Ano,
            t.Status,
            DataInicioInscricoes = t.DataInicioInscricoes.ToString("yyyy-MM-dd"),
            DataFimInscricoes = t.DataFimInscricoes.ToString("yyyy-MM-dd"),
            t.InscricoesHabilitadas,
            t.ValorBonificacaoPorAtleta,
            t.TaxaInscricao,
            Descontos = t.Descontos
                .Select(d => new
                {
                    d.Id,
                    TemporadaCategoriaIds = d.TemporadaCategoriaIds,
                    Categorias = d.TemporadaCategoriaIds.Select(i => t.Categorias.FirstOrDefault(c => c.Id == i)?.Nome ?? "(removida)").OrderBy(n => n).ToList(),
                    d.Tipo,
                    d.Valor
                })
                .OrderBy(d => d.Categorias.Count).ThenBy(d => string.Join('+', d.Categorias)),
            Categorias = t.Categorias.OrderBy(tc => tc.Nome).Select(tc => new
            {
                tc.Id,
                tc.CategoriaId,
                tc.Nome,
                tc.IdadeMinima,
                tc.Sexo,
                tc.AceitaAbaixoIdadeMinima,
                tc.MinimoPeriodosEmQuadra,
                tc.MinimoPeriodosForaQuadra,
                tc.Valor
            })
        });
    }

    [HttpPatch("{id:guid}/taxa-inscricao")]
    public async Task<ActionResult> DefinirTaxa(Guid id, [FromBody] DefinirTaxaInscricaoCommand command, CancellationToken ct)
    {
        command.TemporadaId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPut("{id:guid}/descontos")]
    public async Task<ActionResult> DefinirDesconto(Guid id, [FromBody] DefinirDescontoCommand command, CancellationToken ct)
    {
        command.TemporadaId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpDelete("{id:guid}/descontos/{descontoId:guid}")]
    public async Task<ActionResult> RemoverDesconto(Guid id, Guid descontoId)
    {
        var result = await _mediator.EnviarComando(new RemoverDescontoCommand { TemporadaId = id, DescontoId = descontoId });
        return CustomResponse(result);
    }

    [HttpPost("{id:guid}/gerar-cobrancas")]
    public async Task<ActionResult> GerarCobrancas(Guid id, CancellationToken ct)
    {
        var result = await _mediator.EnviarComando(new GerarCobrancasCommand { TemporadaId = id });
        return CustomResponse(result);
    }

    [HttpPatch("{id:guid}/categorias/{categoriaId:guid}/elegibilidade")]
    public async Task<ActionResult> ConfigurarElegibilidade(Guid id, Guid categoriaId, [FromBody] ConfigurarElegibilidadeCategoriaCommand command, CancellationToken ct)
    {
        command.TemporadaId = id;
        command.CategoriaId = categoriaId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("{id:guid}/categorias/{categoriaId:guid}/valor")]
    public async Task<ActionResult> DefinirValorCategoria(Guid id, Guid categoriaId, [FromBody] DefinirValorCategoriaCommand command, CancellationToken ct)
    {
        command.TemporadaId = id;
        command.CategoriaId = categoriaId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpDelete("{id:guid}/categorias/{categoriaId:guid}")]
    public async Task<ActionResult> RemoverCategoria(Guid id, Guid categoriaId)
    {
        var result = await _mediator.EnviarComando(new RemoverCategoriaDaTemporadaCommand { TemporadaId = id, CategoriaId = categoriaId });
        return CustomResponse(result);
    }

    [HttpPost("{id:guid}/categorias")]
    public async Task<ActionResult> AdicionarCategoria(Guid id, [FromBody] AdicionarCategoriaCommand command, CancellationToken ct)
    {
        command.TemporadaId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }
}
