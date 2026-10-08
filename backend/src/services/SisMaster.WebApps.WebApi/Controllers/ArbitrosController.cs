using Microsoft.AspNetCore.Mvc;
using SisMaster.WebApps.WebApi.Application.Queries;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

/// <summary>Dados da área do árbitro (/arbitros): jogos agendados e ainda não terminados.</summary>
[Route("api/arbitros")]
public class ArbitrosController : MainController
{
    private readonly JogosDoArbitroQuery _jogosQuery;

    public ArbitrosController(JogosDoArbitroQuery jogosQuery) => _jogosQuery = jogosQuery;

    [HttpGet("jogos")]
    public async Task<ActionResult> Jogos(CancellationToken ct) => CustomResponse(await _jogosQuery.ExecutarAsync(ct));
}
