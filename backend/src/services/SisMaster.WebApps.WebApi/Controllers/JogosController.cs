using Microsoft.AspNetCore.Authorization;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApps.WebApi.Seguranca;
using Microsoft.AspNetCore.Mvc;
using SisMaster.Core.Communications;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Application.Queries;
using SisMaster.WebApps.WebApi.Application.Services;
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
    private readonly ConferenciaDeAgendaServico _conferencia;

    public JogosController(IMediatorHandler mediator, ILocalRepository localRepository, IAssociacaoRepository associacaoRepository,
        JogosDaTemporadaQuery jogosQuery, DetalheJogoQuery detalheQuery, PreparoSumulaQuery preparoQuery, ConferenciaDeAgendaServico conferencia)
    {
        _conferencia = conferencia;
        _mediator = mediator;
        _localRepository = localRepository;
        _associacaoRepository = associacaoRepository;
        _jogosQuery = jogosQuery;
        _detalheQuery = detalheQuery;
        _preparoQuery = preparoQuery;
    }

    /// <summary>Detalhe do jogo (Tela 10): equipes, agenda, resultado e resumo da súmula.</summary>
    [Authorize(Policy = Politicas.Gestao)]
    [IdEh(Recurso.Jogo)]
    [HttpGet("jogos/{id:guid}")]
    public async Task<ActionResult> Detalhar(Guid id, CancellationToken ct)
    {
        var detalhe = await _detalheQuery.ExecutarAsync(id, ct);
        return detalhe is null ? NotFound() : CustomResponse(detalhe);
    }

    /// <summary>Registra o W.O. (a equipe ausente perde por 20 × 0, sem bonificação).</summary>
    [Authorize(Policy = Politicas.Gestao)]
    [IdEh(Recurso.Jogo)]
    [HttpPost("jogos/{id:guid}/wo")]
    public async Task<ActionResult> RegistrarWO(Guid id, [FromBody] RegistrarWOCommand command)
    {
        command.JogoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    /// <summary>A súmula do jogo para preparar a relação dos jogadores (Tela 11A).</summary>
    [Authorize(Policy = Politicas.Arbitragem)]
    [IdEh(Recurso.Jogo)]
    [HttpGet("jogos/{id:guid}/sumula")]
    public async Task<ActionResult> ObterSumula(Guid id, CancellationToken ct)
    {
        var preparo = await _preparoQuery.ExecutarAsync(id, ct);
        return preparo is null ? NotFound() : CustomResponse(preparo);
    }

    /// <summary>Cria a súmula do jogo (estado EmPreparacao).</summary>
    [Authorize(Policy = Politicas.Arbitragem)]
    [IdEh(Recurso.Jogo)]
    [HttpPost("jogos/{id:guid}/sumula")]
    public async Task<ActionResult> PrepararSumula(Guid id)
    {
        var result = await _mediator.EnviarComando(new PrepararSumulaCommand { JogoId = id });
        return CustomResponse(result);
    }

    /// <summary>Salva a relação de um dos times (técnico, capitão e jogadores com a camisa do jogo).</summary>
    [Authorize(Policy = Politicas.Arbitragem)]
    [IdEh(Recurso.Jogo)]
    [HttpPut("jogos/{id:guid}/sumula/relacao")]
    public async Task<ActionResult> SalvarRelacao(Guid id, [FromBody] SalvarRelacaoCommand command)
    {
        command.JogoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    /// <summary>Inicia a súmula: exige as duas relações completas.</summary>
    [Authorize(Policy = Politicas.Arbitragem)]
    [IdEh(Recurso.Jogo)]
    [HttpPost("jogos/{id:guid}/sumula/iniciar")]
    public async Task<ActionResult> IniciarSumula(Guid id)
    {
        var result = await _mediator.EnviarComando(new IniciarSumulaCommand { JogoId = id });
        return CustomResponse(result);
    }

    /// <summary>A tabela de jogos da temporada inteira (todas as categorias), por número.</summary>
    [Authorize(Policy = Politicas.Gestao)]
    [HttpGet("temporadas/{temporadaId:guid}/jogos")]
    public async Task<ActionResult> Listar(Guid temporadaId, CancellationToken ct)
    {
        var lista = await _jogosQuery.ExecutarAsync(temporadaId, ct);
        return lista is null ? NotFound() : CustomResponse(lista);
    }

    /// <summary>
    /// Confere, sem gravar, os conflitos de uma agenda proposta: <c>bloqueios</c> (local ocupado, equipe em dois jogos) impedem
    /// salvar; <c>avisos</c> (atleta em duas equipes) só informam. A tela usa para mostrar antes de salvar.
    /// </summary>
    [Authorize(Policy = Politicas.Gestao)]
    [HttpPost("temporadas/{temporadaId:guid}/jogos/conferir-agenda")]
    public async Task<ActionResult> ConferirAgenda(Guid temporadaId, [FromBody] ConferirAgendaRequest request, CancellationToken ct)
    {
        if (request.Itens is not { Count: > 0 and <= 200 })
            return BadRequest("Informe de 1 a 200 jogos");
        var resultado = await _conferencia.ConferirAsync(temporadaId,
            request.Itens.Select(i => new AgendaProposta(i.JogoId, i.Data, i.Hora, i.LocalId)).ToList(), ct);
        return CustomResponse(new { resultado.Ok, resultado.Bloqueios, resultado.Avisos, DuracaoPresumidaMinutos = ConferenciaDeAgenda.DuracaoMinutos });
    }

    public sealed record ConferirAgendaRequest(List<ConferirAgendaItem> Itens);
    public sealed record ConferirAgendaItem(Guid JogoId, DateOnly? Data, TimeOnly? Hora, Guid? LocalId);

    /// <summary>Agenda vários jogos no mesmo dia, com horários em sequência.</summary>
    [Authorize(Policy = Politicas.Gestao)]
    [HttpPatch("temporadas/{temporadaId:guid}/jogos/agendar-lote")]
    public async Task<ActionResult> AgendarEmLote(Guid temporadaId, [FromBody] AgendarJogosEmLoteCommand command)
    {
        command.TemporadaId = temporadaId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    /// <summary>Monta a tabela de jogos da temporada inteira a partir das fases (definitivo).</summary>
    [Authorize(Policy = Politicas.Gestao)]
    [HttpPost("temporadas/{temporadaId:guid}/tabela-jogos")]
    public async Task<ActionResult> GerarTabela(Guid temporadaId)
    {
        var result = await _mediator.EnviarComando(new GerarTabelaJogosCommand { TemporadaId = temporadaId });
        return CustomResponse(result);
    }

    /// <summary>Define data, hora e local do jogo.</summary>
    [Authorize(Policy = Politicas.Gestao)]
    [IdEh(Recurso.Jogo)]
    [HttpPatch("jogos/{id:guid}")]
    public async Task<ActionResult> Agendar(Guid id, [FromBody] AgendarJogoCommand command)
    {
        command.JogoId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [Authorize(Policy = Politicas.Gestao)]
    [HttpGet("associacoes/{associacaoId:guid}/locais")]
    public async Task<ActionResult> ListarLocais(Guid associacaoId, CancellationToken ct)
    {
        var associacao = await _associacaoRepository.ObterPorIdAsync(associacaoId, ct);
        if (associacao is null) return NotFound();

        var locais = await _localRepository.ListarPorAssociacaoAsync(associacaoId, ct);
        var usos = await _localRepository.ContarUsosAsync(associacaoId, ct);
        return CustomResponse(locais.Select(l => new { l.Id, l.Nome, l.Cidade, l.Estado, Jogos = usos.GetValueOrDefault(l.Id) }));
    }

    [Authorize(Policy = Politicas.Gestao)]
    [IdEh(Recurso.Local)]
    [HttpPatch("locais/{id:guid}")]
    public async Task<ActionResult> EditarLocal(Guid id, [FromBody] EditarLocalCommand command)
    {
        command.LocalId = id;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }

    [Authorize(Policy = Politicas.Gestao)]
    [IdEh(Recurso.Local)]
    [HttpDelete("locais/{id:guid}")]
    public async Task<ActionResult> ExcluirLocal(Guid id)
    {
        var result = await _mediator.EnviarComando(new ExcluirLocalCommand { LocalId = id });
        return CustomResponse(result);
    }

    [Authorize(Policy = Politicas.Gestao)]
    [HttpPost("associacoes/{associacaoId:guid}/locais")]
    public async Task<ActionResult> CriarLocal(Guid associacaoId, [FromBody] CriarLocalCommand command)
    {
        command.AssociacaoId = associacaoId;
        var result = await _mediator.EnviarComando(command);
        return CustomResponse(result);
    }
}
