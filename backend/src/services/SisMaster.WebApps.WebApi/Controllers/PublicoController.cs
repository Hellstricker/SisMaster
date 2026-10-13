using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SisMaster.WebApps.WebApi.Application.Queries;

namespace SisMaster.WebApps.WebApi.Controllers;

/// <summary>
/// API do site público: somente leitura, sem login e sem dado pessoal. Responde com cache curto para aguentar o acesso do público.
/// Tudo aqui é de propósito anônimo; qualquer rota nova que mostre dado de pessoa NÃO pertence a este controlador.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/publico")]
public class PublicoController : ControllerBase
{
    private readonly PublicoQuery _query;
    private readonly PublicoVitrineQuery _vitrine;

    public PublicoController(PublicoQuery query, PublicoVitrineQuery vitrine)
    {
        _query = query;
        _vitrine = vitrine;
    }

    private void Cache(int segundos) => Response.Headers.CacheControl = $"public, max-age={segundos}";

    /// <summary>Nome, sigla e UF da associação (o site usa uma associação fixa, definida por configuração).</summary>
    [HttpGet("associacoes/{sigla}")]
    public async Task<ActionResult> Associacao(string sigla, CancellationToken ct)
    {
        var a = await _query.AssociacaoAsync(sigla, ct);
        if (a is null) return NotFound();
        Cache(60);
        return Ok(a);
    }

    /// <summary>Conteúdo das páginas "A associação" e "Contato" (cadastrado pela própria associação).</summary>
    [HttpGet("associacoes/{sigla}/pagina")]
    public async Task<ActionResult> Pagina(string sigla, CancellationToken ct)
    {
        var r = await _query.PaginaAsync(sigla, ct);
        if (r is null) return NotFound();
        Cache(30);
        return Ok(r);
    }

    /// <summary>Início do site: jogos ao vivo, de hoje, próximos e últimos resultados.</summary>
    [HttpGet("associacoes/{sigla}/inicio")]
    public async Task<ActionResult> Inicio(string sigla, CancellationToken ct)
    {
        var r = await _vitrine.InicioAsync(sigla, Application.Services.RelogioLocal.Hoje, ct);
        if (r is null) return NotFound();
        Cache(5);
        return Ok(r);
    }

    /// <summary>Campeonatos (temporadas) da associação.</summary>
    [HttpGet("associacoes/{sigla}/campeonatos")]
    public async Task<ActionResult> Campeonatos(string sigla, CancellationToken ct)
    {
        var r = await _vitrine.CampeonatosAsync(sigla, ct);
        if (r is null) return NotFound();
        Cache(30);
        return Ok(r);
    }

    /// <summary>A temporada: categorias, fases, equipes (só nome e quantidade de atletas) e a tabela de jogos.</summary>
    [HttpGet("temporadas/{id:guid}")]
    public async Task<ActionResult> Temporada(Guid id, CancellationToken ct)
    {
        var r = await _vitrine.TemporadaAsync(id, ct);
        if (r is null) return NotFound();
        Cache(10);
        return Ok(r);
    }

    /// <summary>Classificação da fase (a mesma da gestão).</summary>
    [HttpGet("fases/{id:guid}/classificacao")]
    public async Task<ActionResult> Classificacao(Guid id, CancellationToken ct)
    {
        var r = await _vitrine.ClassificacaoAsync(id, ct);
        if (r is null) return NotFound();
        Cache(10);
        return Ok(r);
    }

    /// <summary>O jogo: placar, parciais por período e a súmula (nome, camisa e estatísticas dos atletas).</summary>
    [HttpGet("jogos/{id:guid}")]
    public async Task<ActionResult> Jogo(Guid id, CancellationToken ct)
    {
        var r = await _vitrine.JogoAsync(id, ct);
        if (r is null) return NotFound();
        Cache(5);
        return Ok(r);
    }

    /// <summary>Temporadas com inscrições abertas hoje, com as categorias e a janela de datas.</summary>
    [HttpGet("associacoes/{sigla}/inscricoes-abertas")]
    public async Task<ActionResult> InscricoesAbertas(string sigla, CancellationToken ct)
    {
        var r = await _query.InscricoesAbertasAsync(sigla, Application.Services.RelogioLocal.Agora, ct);
        if (r is null) return NotFound();
        Cache(30);
        return Ok(r);
    }
}
