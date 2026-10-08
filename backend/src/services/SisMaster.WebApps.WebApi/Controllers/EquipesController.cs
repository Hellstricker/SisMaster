using Microsoft.AspNetCore.Mvc;
using SisMaster.Core.Communications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Participantes;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

[Route("api")]
public class EquipesController : MainController
{
    private readonly IMediatorHandler _mediator;
    private readonly IEquipeRepository _repository;
    private readonly ITemporadaRepository _temporadaRepository;

    public EquipesController(IMediatorHandler mediator, IEquipeRepository repository, ITemporadaRepository temporadaRepository)
    {
        _mediator = mediator;
        _repository = repository;
        _temporadaRepository = temporadaRepository;
    }

    /// <summary>Atleta/candidato a atleta exibido na tela. Id vazio = ainda sem equipe (só existe o pedido efetivado).</summary>
    private sealed record AtletaItem(Guid Id, Guid InscricaoCategoriaId, string Nome, PerfilPessoa Perfil, decimal? SaldoEmAberto);

    private static AtletaItem ProjetarAtleta(Guid id, InscricaoCategoria ic) => new(
        id,
        ic.Id,
        ic.Inscricao.Pessoa.Nome,
        ic.Inscricao.Pessoa.Perfil,
        // Aviso (não bloqueante): cobrança final gerada e ainda com saldo em aberto na ficha.
        ic.Inscricao.CobrancaGerada && (ic.Inscricao.Saldo ?? 0) > 0 ? ic.Inscricao.Saldo : null);

    /// <summary>Tudo que a tela precisa: por categoria, as equipes com elenco e os efetivados ainda sem equipe.</summary>
    [HttpGet("temporadas/{temporadaId:guid}/equipes")]
    public async Task<ActionResult> ListarDaTemporada(Guid temporadaId, CancellationToken ct)
    {
        var temporada = await _temporadaRepository.ObterPorIdAsync(temporadaId, ct);
        if (temporada is null) return NotFound();

        var equipes = await _repository.ListarPorTemporadaAsync(temporadaId, ct);
        var semEquipe = await _repository.ListarEfetivadosSemEquipeAsync(temporadaId, ct);

        var permiteFormar = temporada.Status is StatusTemporada.InscricoesEncerradas or StatusTemporada.EmAndamento;
        return CustomResponse(new
        {
            temporada.Id,
            temporada.Ano,
            temporada.Status,
            PermiteFormarEquipes = permiteFormar,
            Categorias = temporada.Categorias.OrderBy(c => c.Nome).Select(c => new
            {
                TemporadaCategoriaId = c.Id,
                c.Nome,
                Equipes = equipes.Where(e => e.TemporadaCategoriaId == c.Id).Select(e => new
                {
                    e.Id,
                    e.Nome,
                    e.Cor,
                    Atletas = e.Atletas.Select(a => ProjetarAtleta(a.Id, a.InscricaoCategoria)).OrderBy(a => a.Nome)
                }),
                SemEquipe = semEquipe.Where(ic => ic.TemporadaCategoriaId == c.Id).Select(ic => ProjetarAtleta(Guid.Empty, ic))
            })
        });
    }

    [HttpPost("temporadas/{temporadaId:guid}/equipes")]
    public async Task<ActionResult> Criar(Guid temporadaId, [FromBody] CriarEquipeCommand command)
    {
        command.TemporadaId = temporadaId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpPatch("equipes/{id:guid}")]
    public async Task<ActionResult> Editar(Guid id, [FromBody] EditarEquipeCommand command)
    {
        command.EquipeId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpDelete("equipes/{id:guid}")]
    public async Task<ActionResult> Excluir(Guid id)
    {
        var result = await _mediator.EnviarComando(new ExcluirEquipeCommand { EquipeId = id });
        return CustomResponse(result);
    }

    [HttpPost("equipes/{id:guid}/atletas")]
    public async Task<ActionResult> AdicionarAtleta(Guid id, [FromBody] AdicionarAtletaCommand command)
    {
        command.EquipeId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpDelete("equipes/{id:guid}/atletas/{atletaId:guid}")]
    public async Task<ActionResult> RemoverAtleta(Guid id, Guid atletaId)
    {
        var result = await _mediator.EnviarComando(new RemoverAtletaCommand { EquipeId = id, AtletaId = atletaId });
        return CustomResponse(result);
    }
}
