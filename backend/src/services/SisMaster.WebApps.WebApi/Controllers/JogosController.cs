using Microsoft.AspNetCore.Mvc;
using SisMaster.Core.Communications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Application.Queries;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.WebApps.WebApi.Controllers;

[Route("api")]
public class JogosController : MainController
{
    private readonly IMediatorHandler _mediator;
    private readonly ILocalRepository _localRepository;
    private readonly IAssociacaoRepository _associacaoRepository;
    private readonly JogosDaTemporadaQuery _jogosQuery;
    private readonly DetalheJogoQuery _detalheQuery;
    private readonly PreparoSumulaQuery _preparoQuery;

    public JogosController(IMediatorHandler mediator, ILocalRepository localRepository, IAssociacaoRepository associacaoRepository,
        JogosDaTemporadaQuery jogosQuery, DetalheJogoQuery detalheQuery, PreparoSumulaQuery preparoQuery)
    {
        _mediator = mediator;
        _localRepository = localRepository;
        _associacaoRepository = associacaoRepository;
        _jogosQuery = jogosQuery;
        _detalheQuery = detalheQuery;
        _preparoQuery = preparoQuery;
    }

    /// <summary>Detalhe do jogo (Tela 10): equipes, agenda, resultado e resumo da súmula.</summary>
    [HttpGet("jogos/{id:guid}")]
    public async Task<ActionResult> Detalhar(Guid id, CancellationToken ct)
    {
        var detalhe = await _detalheQuery.ExecutarAsync(id, ct);
        return detalhe is null ? NotFound() : CustomResponse(detalhe);
    }

    /// <summary>Registra o W.O. (a equipe ausente perde por 20 × 0, sem bonificação).</summary>
    [HttpPost("jogos/{id:guid}/wo")]
    public async Task<ActionResult> RegistrarWO(Guid id, [FromBody] RegistrarWOCommand command)
    {
        command.JogoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    /// <summary>A súmula do jogo para preparar a relação dos jogadores (Tela 11A).</summary>
    [HttpGet("jogos/{id:guid}/sumula")]
    public async Task<ActionResult> ObterSumula(Guid id, CancellationToken ct)
    {
        var preparo = await _preparoQuery.ExecutarAsync(id, ct);
        return preparo is null ? NotFound() : CustomResponse(preparo);
    }

    /// <summary>Cria a súmula do jogo (estado EmPreparacao).</summary>
    [HttpPost("jogos/{id:guid}/sumula")]
    public async Task<ActionResult> PrepararSumula(Guid id)
    {
        var result = await _mediator.EnviarComando(new PrepararSumulaCommand { JogoId = id });
        return CustomResponse(result);
    }

    /// <summary>Salva a relação de um dos times (técnico, capitão e jogadores com a camisa do jogo).</summary>
    [HttpPut("jogos/{id:guid}/sumula/relacao")]
    public async Task<ActionResult> SalvarRelacao(Guid id, [FromBody] SalvarRelacaoCommand command)
    {
        command.JogoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    /// <summary>Inicia a súmula: exige as duas relações completas.</summary>
    [HttpPost("jogos/{id:guid}/sumula/iniciar")]
    public async Task<ActionResult> IniciarSumula(Guid id)
    {
        var result = await _mediator.EnviarComando(new IniciarSumulaCommand { JogoId = id });
        return CustomResponse(result);
    }

    /// <summary>A tabela de jogos da temporada inteira (todas as categorias), por número.</summary>
    [HttpGet("temporadas/{temporadaId:guid}/jogos")]
    public async Task<ActionResult> Listar(Guid temporadaId, CancellationToken ct)
    {
        var lista = await _jogosQuery.ExecutarAsync(temporadaId, ct);
        return lista is null ? NotFound() : CustomResponse(lista);
    }

    /// <summary>Agenda vários jogos no mesmo dia, com horários em sequência.</summary>
    [HttpPatch("temporadas/{temporadaId:guid}/jogos/agendar-lote")]
    public async Task<ActionResult> AgendarEmLote(Guid temporadaId, [FromBody] AgendarJogosEmLoteCommand command)
    {
        command.TemporadaId = temporadaId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    /// <summary>Monta a tabela de jogos da temporada inteira a partir das fases (definitivo).</summary>
    [HttpPost("temporadas/{temporadaId:guid}/tabela-jogos")]
    public async Task<ActionResult> GerarTabela(Guid temporadaId)
    {
        var result = await _mediator.EnviarComando(new GerarTabelaJogosCommand { TemporadaId = temporadaId });
        return CustomResponse(result);
    }

    /// <summary>Define data, hora e local do jogo.</summary>
    [HttpPatch("jogos/{id:guid}")]
    public async Task<ActionResult> Agendar(Guid id, [FromBody] AgendarJogoCommand command)
    {
        command.JogoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpGet("associacoes/{associacaoId:guid}/locais")]
    public async Task<ActionResult> ListarLocais(Guid associacaoId, CancellationToken ct)
    {
        var associacao = await _associacaoRepository.ObterPorIdAsync(associacaoId, ct);
        if (associacao is null) return NotFound();

        var locais = await _localRepository.ListarPorAssociacaoAsync(associacaoId, ct);
        var usos = await _localRepository.ContarUsosAsync(associacaoId, ct);
        return CustomResponse(locais.Select(l => new { l.Id, l.Nome, l.Cidade, l.Estado, Jogos = usos.GetValueOrDefault(l.Id) }));
    }

    [HttpPatch("locais/{id:guid}")]
    public async Task<ActionResult> EditarLocal(Guid id, [FromBody] EditarLocalCommand command)
    {
        command.LocalId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [HttpDelete("locais/{id:guid}")]
    public async Task<ActionResult> ExcluirLocal(Guid id)
    {
        var result = await _mediator.EnviarComando(new ExcluirLocalCommand { LocalId = id });
        return CustomResponse(result);
    }

    [HttpPost("associacoes/{associacaoId:guid}/locais")]
    public async Task<ActionResult> CriarLocal(Guid associacaoId, [FromBody] CriarLocalCommand command)
    {
        command.AssociacaoId = associacaoId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }
}
