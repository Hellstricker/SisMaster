using Microsoft.AspNetCore.Mvc;
using SisMaster.Core.Communications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

[Route("api/associacoes")]
public class AssociacoesController : MainController
{
    private readonly IMediatorHandler _mediator;
    private readonly IAssociacaoRepository _repository;
    private readonly ICategoriaRepository _categoriaRepository;

    public AssociacoesController(IMediatorHandler mediator, IAssociacaoRepository repository, ICategoriaRepository categoriaRepository)
    {
        _mediator = mediator;
        _repository = repository;
        _categoriaRepository = categoriaRepository;
    }

    [HttpGet]
    public async Task<ActionResult> Listar(CancellationToken ct)
    {
        var associacoes = await _repository.ListarAsync(ct);
        return CustomResponse(associacoes.Select(a => new
        {
            a.Id,
            a.Nome,
            a.Sigla,
            a.Uf,
            a.Ativa,
            TotalCampeonatos = a.Campeonatos.Count
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var a = await _repository.ObterPorIdAsync(id, ct);
        if (a is null) return NotFound();
        return CustomResponse(new
        {
            a.Id,
            a.Nome,
            a.Sigla,
            a.Uf,
            a.Ativa,
            Campeonatos = a.Campeonatos.Select(c => new { c.Id, c.Nome })
        });
    }

    [HttpPost]
    public async Task<ActionResult> Criar([FromBody] CriarAssociacaoCommand command, CancellationToken ct)
    {
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult> Editar(Guid id, [FromBody] EditarAssociacaoCommand command, CancellationToken ct)
    {
        command.AssociacaoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult> AlterarStatus(Guid id, [FromBody] AlterarStatusAssociacaoCommand command, CancellationToken ct)
    {
        command.AssociacaoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpGet("{id:guid}/categorias")]
    public async Task<ActionResult> ListarCategorias(Guid id, CancellationToken ct)
    {
        var categorias = await _categoriaRepository.ObterPorAssociacaoAsync(id, ct);
        var uso = await _categoriaRepository.ContarUsoPorAssociacaoAsync(id, ct);
        return CustomResponse(categorias.OrderBy(c => c.IdadeMinima).ThenBy(c => c.Nome).Select(c => new
        {
            c.Id,
            c.Nome,
            Uso = uso.GetValueOrDefault(c.Id),
            c.IdadeMinima,
            c.Sexo,
            c.AceitaAbaixoIdadeMinima,
            c.MinimoPeriodosEmQuadra,
            c.MinimoPeriodosForaQuadra
        }));
    }

    [HttpPatch("{id:guid}/categorias/{categoriaId:guid}")]
    public async Task<ActionResult> EditarCategoria(Guid id, Guid categoriaId, [FromBody] EditarCategoriaCommand command, CancellationToken ct)
    {
        command.AssociacaoId = id;
        command.CategoriaId = categoriaId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpDelete("{id:guid}/categorias/{categoriaId:guid}")]
    public async Task<ActionResult> ExcluirCategoria(Guid id, Guid categoriaId, CancellationToken ct)
    {
        var result = await _mediator.EnviarComando(new ExcluirCategoriaCommand { AssociacaoId = id, CategoriaId = categoriaId });
        return CustomResponse(result);
    }

    [HttpPost("{id:guid}/categorias")]
    public async Task<ActionResult> CriarCategoria(Guid id, [FromBody] CriarCategoriaCommand command, CancellationToken ct)
    {
        command.AssociacaoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPost("{id:guid}/campeonatos")]
    public async Task<ActionResult> CriarCampeonato(Guid id, [FromBody] CriarCampeonatoCommand command, CancellationToken ct)
    {
        command.AssociacaoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }
}
