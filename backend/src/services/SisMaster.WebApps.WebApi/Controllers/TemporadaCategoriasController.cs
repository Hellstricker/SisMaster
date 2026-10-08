using Microsoft.AspNetCore.Mvc;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

[Route("api/temporadas-categorias")]
public class TemporadaCategoriasController : MainController
{
    private readonly ITemporadaRepository _repository;

    public TemporadaCategoriasController(ITemporadaRepository repository) => _repository = repository;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var tc = await _repository.ObterCategoriaPorIdAsync(id, ct);
        if (tc is null) return NotFound();

        var t = tc.Temporada;
        return CustomResponse(new
        {
            tc.Id,
            tc.TemporadaId,
            t.CampeonatoId,
            t.Campeonato.AssociacaoId,
            tc.CategoriaId,
            tc.Nome,
            tc.IdadeMinima,
            tc.Sexo,
            tc.AceitaAbaixoIdadeMinima,
            tc.MinimoPeriodosEmQuadra,
            tc.MinimoPeriodosForaQuadra,
            tc.Valor,
            Temporada = new
            {
                t.Ano,
                t.Status,
                t.InscricoesHabilitadas,
                t.TaxaInscricao,
                Descontos = t.Descontos
                    .Select(d => new { d.Id, d.TemporadaCategoriaIds, d.Tipo, d.Valor })
            }
        });
    }
}
