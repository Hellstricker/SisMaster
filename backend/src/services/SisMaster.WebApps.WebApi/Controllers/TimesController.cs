using Microsoft.AspNetCore.Mvc;
using SisMaster.Core.Communications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Domain.Time;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

[Route("api/times")]
public class TimesController : MainController
{
    private readonly IMediatorHandler _mediator;
    private readonly ITimeRepository _timeRepository;

    public TimesController(IMediatorHandler mediator, ITimeRepository timeRepository)
    {
        _mediator = mediator;
        _timeRepository = timeRepository;
    }

    [HttpGet]
    public async Task<ActionResult> Listar(CancellationToken ct)
    {
        var times = await _timeRepository.ListarAsync(ct);
        return CustomResponse(times.Select(t => new
        {
            t.Id,
            t.Nome,
            t.Sigla,
            t.Ativo,
            TotalJogadores = t.Jogadores.Count
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var time = await _timeRepository.ObterPorIdAsync(id, ct);
        if (time is null) return NotFound();

        return CustomResponse(new
        {
            time.Id,
            time.Nome,
            time.Sigla,
            time.Ativo,
            Jogadores = time.Jogadores.Select(j => new
            {
                j.Id,
                j.Numero,
                j.Ativo,
                j.Pessoa.Nome,
                Cpf = j.Pessoa.Cpf.ToString()
            })
        });
    }

    [HttpPost]
    public async Task<ActionResult> Criar([FromBody] CriarTimeCommand command, CancellationToken ct)
    {
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPost("{id:guid}/jogadores")]
    public async Task<ActionResult> AdicionarJogador(Guid id, [FromBody] AdicionarJogadorCommand command, CancellationToken ct)
    {
        command.TimeId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpGet("{id:guid}/jogadores")]
    public async Task<ActionResult> ListarJogadores(Guid id, CancellationToken ct)
    {
        var time = await _timeRepository.ObterPorIdAsync(id, ct);
        if (time is null) return NotFound();

        var jogadores = time.Jogadores
            .Where(j => j.Ativo)
            .OrderBy(j => j.Numero)
            .Select(j => new
            {
                j.Id,
                j.Numero,
                j.Pessoa.Nome
            });

        return CustomResponse(jogadores);
    }
}
