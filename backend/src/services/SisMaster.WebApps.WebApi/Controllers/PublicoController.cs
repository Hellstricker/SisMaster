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

    public PublicoController(PublicoQuery query) => _query = query;

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

    /// <summary>Temporadas com inscrições abertas hoje, com as categorias e a janela de datas.</summary>
    [HttpGet("associacoes/{sigla}/inscricoes-abertas")]
    public async Task<ActionResult> InscricoesAbertas(string sigla, CancellationToken ct)
    {
        var r = await _query.InscricoesAbertasAsync(sigla, DateTime.Now, ct);
        if (r is null) return NotFound();
        Cache(30);
        return Ok(r);
    }
}
