using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SisMaster.Core.Communications;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApi.Core.Controllers;
using SisMaster.WebApi.Core.Usuarios;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Seguranca;

namespace SisMaster.WebApps.WebApi.Controllers;

public sealed record AtletaDeEquipeRequest(string? Nome, string? Cpf, DateOnly Nascimento, Sexo Sexo, string? Email)
{
    public AtletaConvidado Converter() => new(Nome ?? string.Empty, Cpf ?? string.Empty, Nascimento, Sexo, Email ?? string.Empty);
}

/// <summary>A inscrição de equipe em torneio aberto, do lado do responsável (conta do site, perfil Participante).</summary>
[Route("api/participante")]
[Authorize(Policy = Politicas.Participante)]
public class EquipesDoParticipanteController : MainController
{
    private readonly InscricaoDeEquipeServico _servico;
    private readonly ConviteDeAtletaServico _convites;
    private readonly IAcessoDoUsuario _acesso;

    public EquipesDoParticipanteController(InscricaoDeEquipeServico servico, ConviteDeAtletaServico convites, IAcessoDoUsuario acesso)
    {
        _servico = servico;
        _convites = convites;
        _acesso = acesso;
    }

    private string? ContaId => _acesso.Id;
    private string? ContaEmail => User.FindFirst("email")?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;

    public sealed record EquipeRequest(Guid TemporadaId, Guid TemporadaCategoriaId, Guid CodigoDeEnvio, string? Nome, string? ResponsavelNome, string? ResponsavelTelefone,
        bool ResponsavelJoga, AtletaDeEquipeRequest? Responsavel, List<AtletaDeEquipeRequest>? Atletas);

    public sealed record ConfirmacaoRequest(string? Telefone, int? AlturaCm, int? PesoKg, string? Posicao, bool PossuiPlanoSaude, string? NomePlanoSaude,
        bool AceiteDados, bool AceiteImagem, string? VersaoTermos);

    private async Task<ActionResult> Executar(Func<Task<object?>> acao)
    {
        if (ContaId is null || ContaEmail is null) return Unauthorized();
        try
        {
            var r = await acao();
            return r is null ? NotFound() : CustomResponse(r);
        }
        catch (DomainException ex) when (ex.Message == InscricaoDeEquipeServico.NaoEncontrada)
        {
            return NotFound();
        }
        catch (DomainException ex)
        {
            AdicionarErro(ex.Message);
            return CustomResponse();
        }
    }

    [HttpPost("equipes")]
    public Task<ActionResult> Criar([FromBody] EquipeRequest c, CancellationToken ct) => Executar(async () =>
    {
        var i = await _servico.CriarAsync(ContaId!, ContaEmail!, new DadosDaEquipe(c.TemporadaId, c.TemporadaCategoriaId, c.Nome ?? string.Empty,
            c.ResponsavelNome ?? string.Empty, c.ResponsavelTelefone, c.ResponsavelJoga, c.Responsavel?.Converter(),
            (c.Atletas ?? []).Select(a => a.Converter()).ToList(), c.CodigoDeEnvio), RelogioLocal.Agora, ct);
        return new { i.Id, i.Protocolo, i.Nome };
    });

    [HttpGet("equipes")]
    public Task<ActionResult> Minhas(CancellationToken ct) => Executar(async () => await _servico.ListarMinhasAsync(ContaId!, ct));

    [IdEh(Recurso.InscricaoDeEquipe)]
    [HttpGet("equipes/{id:guid}")]
    public Task<ActionResult> Detalhe(Guid id, CancellationToken ct) => Executar(async () => await _servico.DetalharMinhaAsync(ContaId!, id, ct));

    [IdEh(Recurso.InscricaoDeEquipe)]
    [HttpPost("equipes/{id:guid}/atletas")]
    public Task<ActionResult> AdicionarAtleta(Guid id, [FromBody] AtletaDeEquipeRequest a, CancellationToken ct) => Executar(async () =>
    {
        await _servico.AdicionarAtletaAsync(ContaId!, id, a.Converter(), RelogioLocal.Agora, ct);
        return new { Ok = true };
    });

    [IdEh(Recurso.InscricaoDeEquipe)]
    [HttpPost("equipes/{id:guid}/atletas/{conviteId:guid}/reenviar")]
    public Task<ActionResult> Reenviar(Guid id, Guid conviteId, CancellationToken ct) => Executar(async () =>
    {
        await _servico.ReenviarAsync(ContaId!, id, conviteId, RelogioLocal.Agora, ct);
        return new { Ok = true };
    });

    [IdEh(Recurso.InscricaoDeEquipe)]
    [HttpDelete("equipes/{id:guid}/atletas/{conviteId:guid}")]
    public Task<ActionResult> Remover(Guid id, Guid conviteId, CancellationToken ct) => Executar(async () =>
    {
        await _servico.RemoverAsync(ContaId!, id, conviteId, ct);
        return new { Ok = true };
    });

    /// <summary>O atleta confirma o convite com a conta do e-mail convidado: completa a ficha e dá as duas autorizações.</summary>
    [EnableRateLimiting("vinculo")]
    [HttpPost("convites/{codigo}/confirmar")]
    public Task<ActionResult> Confirmar(string codigo, [FromBody] ConfirmacaoRequest c, CancellationToken ct) => Executar(async () =>
    {
        var i = await _convites.ConfirmarAsync(ContaId!, ContaEmail!, codigo, c.Telefone,
            new FichaDoSite(c.AlturaCm, c.PesoKg, c.Posicao, c.PossuiPlanoSaude, c.NomePlanoSaude), c.AceiteDados, c.AceiteImagem,
            c.VersaoTermos ?? string.Empty, RelogioLocal.Agora, ct);
        return new { i.Protocolo, InscricaoId = i.Id };
    });
}

/// <summary>O link do convite, sem login: só o mínimo para o atleta saber do que se trata, e a recusa.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/convites")]
public class ConvitesController : ControllerBase
{
    private readonly ConviteDeAtletaServico _servico;

    public ConvitesController(ConviteDeAtletaServico servico) => _servico = servico;

    [EnableRateLimiting("convite")]
    [HttpGet("{codigo}")]
    public async Task<ActionResult> Consultar(string codigo, CancellationToken ct)
    {
        var r = await _servico.ConsultarAsync(codigo, RelogioLocal.Agora, ct);
        return r is null ? NotFound() : Ok(r);
    }

    [EnableRateLimiting("convite")]
    [HttpPost("{codigo}/recusar")]
    public async Task<ActionResult> Recusar(string codigo, CancellationToken ct)
    {
        try
        {
            await _servico.RecusarAsync(codigo, ct);
            return Ok(new { Ok = true });
        }
        catch (DomainException ex)
        {
            return NotFound(new { errors = new[] { ex.Message } });
        }
    }
}

/// <summary>A análise das equipes inscritas pela diretoria: aprovar, recusar e registrar a taxa de cada atleta.</summary>
[Route("api")]
[Authorize(Policy = Politicas.Gestao)]
public class EquipesInscritasController : MainController
{
    private readonly InscricaoDeEquipeServico _servico;
    private readonly IMediatorHandler _mediator;
    private readonly IAcessoDoUsuario _acesso;

    public EquipesInscritasController(InscricaoDeEquipeServico servico, IMediatorHandler mediator, IAcessoDoUsuario acesso)
    {
        _servico = servico;
        _mediator = mediator;
        _acesso = acesso;
    }

    public sealed record AprovarRequest(string? JustificativaExcecao);
    public sealed record RecusarRequest(string? Motivo);
    public sealed record TaxasRequest(DateOnly DataPagamento, List<Guid>? InscricaoIds);

    private string? Usuario => User.FindFirst("email")?.Value ?? _acesso.Id;

    private async Task<ActionResult> Executar(Func<Task<object?>> acao)
    {
        try
        {
            var r = await acao();
            return r is null ? NotFound() : CustomResponse(r);
        }
        catch (DomainException ex)
        {
            AdicionarErro(ex.Message);
            return CustomResponse();
        }
    }

    [HttpGet("temporadas/{temporadaId:guid}/equipes-inscritas")]
    public Task<ActionResult> Listar(Guid temporadaId, CancellationToken ct) => Executar(async () => await _servico.ListarDaTemporadaAsync(temporadaId, ct));

    [HttpGet("equipes-inscritas/{inscricaoDeEquipeId:guid}")]
    public Task<ActionResult> Detalhar(Guid inscricaoDeEquipeId, CancellationToken ct) => Executar(async () => await _servico.DetalharParaDiretoriaAsync(inscricaoDeEquipeId, ct));

    [HttpPost("equipes-inscritas/{inscricaoDeEquipeId:guid}/aprovar")]
    public Task<ActionResult> Aprovar(Guid inscricaoDeEquipeId, [FromBody] AprovarRequest c, CancellationToken ct) => Executar(async () =>
    {
        await _servico.AprovarAsync(inscricaoDeEquipeId, c.JustificativaExcecao, Usuario, ct);
        return new { Ok = true };
    });

    [HttpPost("equipes-inscritas/{inscricaoDeEquipeId:guid}/recusar")]
    public Task<ActionResult> Recusar(Guid inscricaoDeEquipeId, [FromBody] RecusarRequest c, CancellationToken ct) => Executar(async () =>
    {
        await _servico.RecusarAsync(inscricaoDeEquipeId, c.Motivo ?? string.Empty, Usuario, ct);
        return new { Ok = true };
    });

    /// <summary>Registra a taxa de vários atletas de uma vez (cada um entra no elenco ao ser efetivado). Os que falharem voltam com o motivo, sem desfazer os demais.</summary>
    [HttpPost("equipes-inscritas/{inscricaoDeEquipeId:guid}/taxas")]
    public Task<ActionResult> RegistrarTaxas(Guid inscricaoDeEquipeId, [FromBody] TaxasRequest c, CancellationToken ct) => Executar(async () =>
    {
        var detalhe = await _servico.DetalharParaDiretoriaAsync(inscricaoDeEquipeId, ct);
        if (detalhe is null) return null;
        var permitidos = await _servico.FichasDaEquipeAsync(inscricaoDeEquipeId, ct);
        var resultados = new List<object>();
        foreach (var fichaId in (c.InscricaoIds ?? []).Distinct())
        {
            if (!permitidos.Contains(fichaId)) { resultados.Add(new { InscricaoId = fichaId, Ok = false, Erro = "Atleta não pertence a esta equipe" }); continue; }
            var r = await _mediator.EnviarComando(new RegistrarPagamentoTaxaCommand { InscricaoId = fichaId, DataPagamento = c.DataPagamento, AvisarPorEmail = true });
            resultados.Add(new { InscricaoId = fichaId, Ok = r.IsValid, Erro = r.IsValid ? null : string.Join("; ", r.Errors.Select(e => e.ErrorMessage)) });
        }
        return new { Resultados = resultados };
    });
}
