using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApi.Core.Controllers;
using SisMaster.WebApi.Core.Usuarios;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Controllers;

/// <summary>
/// Rotas da conta do público (perfil Participante): ligar a conta ao cadastro e se inscrever. Só o próprio participante as usa; a equipe
/// (Administrador, Árbitro...) recebe 403. A conta e o e-mail vêm do token (confirmado no cadastro), nunca do corpo da requisição.
/// </summary>
[Route("api/participante")]
[Authorize(Policy = Politicas.Participante)]
public class ParticipanteController : MainController
{
    private readonly InscricaoDoParticipanteServico _servico;
    private readonly IAcessoDoUsuario _acesso;

    public ParticipanteController(InscricaoDoParticipanteServico servico, IAcessoDoUsuario acesso)
    {
        _servico = servico;
        _acesso = acesso;
    }

    private string? ContaId => _acesso.Id;
    private string? ContaEmail => User.FindFirst("email")?.Value ?? User.FindFirst(ClaimTypes.Email)?.Value;

    public sealed record VinculoRequest(string? Nome, string? Cpf, DateOnly Nascimento, Sexo Sexo, string? Telefone);

    public sealed record EnvioRequest(Guid TemporadaId, Guid CodigoDeEnvio, int? AlturaCm, int? PesoKg, string? Posicao, bool PossuiPlanoSaude,
        string? NomePlanoSaude, bool AceiteDados, bool AceiteImagem, string? VersaoTermos, List<Guid>? CategoriaIds);

    /// <summary>O que o formulário precisa: a temporada, o cadastro ligado (se houver), a última ficha e as categorias (com os pedidos já ativos).</summary>
    [HttpGet("temporadas/{temporadaId:guid}/inscricao/preparo")]
    public async Task<ActionResult> Preparo(Guid temporadaId, CancellationToken ct)
    {
        if (ContaId is null) return Unauthorized();
        var r = await _servico.PrepararAsync(ContaId, temporadaId, RelogioLocal.Agora, ct);
        return r is null ? NotFound() : CustomResponse(r);
    }

    /// <summary>
    /// Liga a conta ao cadastro pelo CPF. Resultado "Vinculada" (com o cadastro para conferência), "Menor" (a diretoria inscreve) ou
    /// "Bloqueada" (o CPF pertence a outro cadastro: fale com a diretoria; nada do cadastro é revelado). Poucas tentativas por conta.
    /// </summary>
    [EnableRateLimiting("vinculo")]
    [HttpPost("vinculo")]
    public async Task<ActionResult> Vincular([FromBody] VinculoRequest corpo, CancellationToken ct)
    {
        if (ContaId is null || ContaEmail is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(corpo.Nome) || corpo.Nome.Trim().Length < 3)
        {
            AdicionarErro("Informe o nome completo.");
            return CustomResponse();
        }
        try
        {
            var r = await _servico.VincularAsync(ContaId, ContaEmail,
                new DadosDaPessoaDoSite(corpo.Nome, corpo.Cpf ?? string.Empty, corpo.Nascimento, corpo.Sexo, corpo.Telefone), RelogioLocal.Hoje, ct);
            return CustomResponse(new
            {
                Situacao = r.Situacao.ToString(),
                Pessoa = r.Pessoa is null ? null : new
                {
                    r.Pessoa.Nome,
                    CpfMascarado = InscricaoDoParticipanteServico.Mascarar(r.Pessoa.Cpf.Numero),
                    Nascimento = r.Pessoa.Nascimento.ToString("yyyy-MM-dd"),
                    Sexo = r.Pessoa.Sexo.ToString(),
                    r.Pessoa.Telefone
                }
            });
        }
        catch (DomainException ex)
        {
            AdicionarErro(ex.Message);
            return CustomResponse();
        }
    }

    /// <summary>Envia a inscrição. Reenviar com o mesmo código de envio devolve a mesma inscrição, sem duplicar.</summary>
    [HttpPost("inscricoes")]
    public async Task<ActionResult> Enviar([FromBody] EnvioRequest corpo, CancellationToken ct)
    {
        if (ContaId is null) return Unauthorized();
        try
        {
            var i = await _servico.EnviarAsync(ContaId, corpo.TemporadaId, corpo.CodigoDeEnvio,
                new FichaDoSite(corpo.AlturaCm, corpo.PesoKg, corpo.Posicao, corpo.PossuiPlanoSaude, corpo.NomePlanoSaude),
                corpo.AceiteDados, corpo.AceiteImagem, corpo.VersaoTermos ?? string.Empty, corpo.CategoriaIds ?? [], RelogioLocal.Agora, ct);
            return CustomResponse(new { i.Protocolo, EnviadaEm = i.DataEnvio, Categorias = i.Categorias.Count });
        }
        catch (DomainException ex)
        {
            AdicionarErro(ex.Message);
            return CustomResponse();
        }
    }
}
