using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SisMaster.Identidade.Api.Email;
using SisMaster.Identidade.Api.Services;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApi.Core.Controllers;

namespace SisMaster.Identidade.Api.Controllers;

public class NovoCadastro
{
    [Required(ErrorMessage = "Informe o nome completo."), MinLength(3, ErrorMessage = "O nome precisa de pelo menos 3 letras."), MaxLength(120)]
    public string? Nome { get; set; }

    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), MaxLength(200)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Informe a senha.")]
    public string? Senha { get; set; }

    /// <summary>Aceite dos Termos de uso e da Política de privacidade (obrigatório), com a versão que a pessoa viu.</summary>
    public bool AceiteTermos { get; set; }
    public string? VersaoTermos { get; set; }

    /// <summary>Caminho do site para onde voltar depois de confirmar o e-mail (ex.: /inscricao/abc). Só caminhos internos valem.</summary>
    public string? Voltar { get; set; }

    /// <summary>Campo-isca: invisível para pessoas; robôs preenchem. Preenchido, o cadastro é ignorado em silêncio.</summary>
    public string? Site { get; set; }
}

public class ConfirmacaoDeEmailPedido
{
    [Required(ErrorMessage = "Link inválido: falta o e-mail.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Link inválido: falta o código.")]
    public string? Token { get; set; }
}

public class ReenvioDeConfirmacaoPedido
{
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string? Email { get; set; }

    public string? Voltar { get; set; }
}

/// <summary>
/// Conta do público (perfil Participante): cadastro aberto, e-mail confirmado obrigatório. Nenhuma resposta revela se um e-mail já
/// tem conta; o cadastro tem limite por endereço e um campo-isca contra robôs.
/// </summary>
[Route("api/identidade")]
public class CadastroController : MainController
{
    public const string MensagemDoCadastro = "Se o e-mail ainda não tiver conta, enviamos um link para confirmá-lo. Abra o e-mail e clique no link (vale por 24 horas).";
    public const string MensagemDoReenvio = "Se o e-mail tiver uma conta ainda não confirmada, enviamos um novo link.";
    public const string LinkInvalido = "Este link é inválido ou já venceu. Peça um novo na tela de entrada.";

    private readonly UserManager<IdentityUser> _userManager;
    private readonly IEnviadorDeEmail _email;
    private readonly EmissorDeToken _emissor;
    private readonly ILogger<CadastroController> _log;
    private readonly IConfiguration _configuracao;

    public CadastroController(UserManager<IdentityUser> userManager, IEnviadorDeEmail email, EmissorDeToken emissor, ILogger<CadastroController> log, IConfiguration configuracao)
    {
        _userManager = userManager;
        _email = email;
        _emissor = emissor;
        _log = log;
        _configuracao = configuracao;
    }

    private string RaizDoSite => (_configuracao["App:UrlPublica"] ?? "http://localhost:3000").TrimEnd('/');

    /// <summary>Cria a conta do participante e envia o link de confirmação do e-mail. A resposta é sempre a mesma.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("cadastro")]
    [HttpPost("cadastrar")]
    public async Task<IActionResult> Cadastrar([FromBody] NovoCadastro model)
    {
        if (!ModelState.IsValid) return CustomResponse(ModelState);
        if (!model.AceiteTermos)
        {
            AdicionarErro("É preciso aceitar os Termos de uso e a Política de privacidade para criar a conta.");
            return CustomResponse();
        }

        // Robô: preencheu o campo invisível. Responde igual, mas não cria nada nem envia e-mail.
        if (!string.IsNullOrWhiteSpace(model.Site)) return CustomResponse(new { mensagem = MensagemDoCadastro });

        // A senha é conferida antes de ver se o e-mail existe: o erro dela não revela nada sobre a conta.
        var erros = new List<string>();
        foreach (var validador in _userManager.PasswordValidators)
        {
            var r = await validador.ValidateAsync(_userManager, new IdentityUser { UserName = model.Email, Email = model.Email }, model.Senha!);
            if (!r.Succeeded) erros.AddRange(r.Errors.Select(e => e.Description));
        }
        if (erros.Count > 0)
        {
            AdicionarErros(erros.Distinct().ToArray());
            return CustomResponse();
        }

        var endereco = model.Email!.Trim();
        var voltar = RegrasDaContaDoPublico.CaminhoDeRetornoSeguro(model.Voltar);
        var existente = await _userManager.FindByEmailAsync(endereco);
        if (existente is not null)
        {
            await AvisarQueJaTemContaAsync(existente);
            return CustomResponse(new { mensagem = MensagemDoCadastro });
        }

        var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var usuario = new IdentityUser { UserName = endereco, Email = endereco, EmailConfirmed = false };
        var criado = await _userManager.CreateAsync(usuario, model.Senha!);
        if (!criado.Succeeded)
        {
            // Corrida entre dois cadastros do mesmo e-mail: a resposta continua a mesma.
            _log.LogWarning("Cadastro recusado: {Erros}", string.Join("; ", criado.Errors.Select(e => e.Code)));
            return CustomResponse(new { mensagem = MensagemDoCadastro });
        }
        await _userManager.AddToRoleAsync(usuario, Perfis.Participante);
        await _userManager.AddClaimsAsync(usuario,
        [
            new(TiposDeClaim.Nome, model.Nome!.Trim()),
            new(TiposDeClaim.CriadaEm, agora),
            new(TiposDeClaim.TermosVersao, string.IsNullOrWhiteSpace(model.VersaoTermos) ? "?" : model.VersaoTermos!.Trim()),
            new(TiposDeClaim.TermosEm, agora),
        ]);
        await EnviarConfirmacaoAsync(usuario, model.Nome!.Trim(), voltar);
        return CustomResponse(new { mensagem = MensagemDoCadastro });
    }

    /// <summary>
    /// Confirma o e-mail com o link recebido. Link de uso único e válido por 24 horas. Dá certo: a pessoa já sai conectada.
    /// Link inválido, vencido ou já usado dá sempre a mesma mensagem.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("confirmar-email")]
    public async Task<IActionResult> Confirmar([FromBody] ConfirmacaoDeEmailPedido model)
    {
        if (!ModelState.IsValid) return CustomResponse(ModelState);

        var user = await _userManager.FindByEmailAsync(model.Email!.Trim());
        if (user is null || user.EmailConfirmed || !await _userManager.IsInRoleAsync(user, Perfis.Participante) || await Desativado(user))
        {
            AdicionarErro(LinkInvalido);
            return CustomResponse();
        }

        var resultado = await _userManager.ConfirmEmailAsync(user, model.Token!);
        if (!resultado.Succeeded)
        {
            AdicionarErro(LinkInvalido);
            return CustomResponse();
        }
        return CustomResponse(await _emissor.GerarAsync(user));
    }

    /// <summary>Reenvia o link de confirmação (no máximo um a cada 10 minutos por conta). A resposta é sempre a mesma.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("cadastro")]
    [HttpPost("reenviar-confirmacao")]
    public async Task<IActionResult> Reenviar([FromBody] ReenvioDeConfirmacaoPedido model)
    {
        if (!ModelState.IsValid) return CustomResponse(ModelState);

        var user = await _userManager.FindByEmailAsync(model.Email!.Trim());
        if (user is not null && !user.EmailConfirmed && await _userManager.IsInRoleAsync(user, Perfis.Participante) && !await Desativado(user))
        {
            var claims = await _userManager.GetClaimsAsync(user);
            var ultimo = claims.FirstOrDefault(c => c.Type == TiposDeClaim.ConfirmacaoEm)?.Value;
            var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!long.TryParse(ultimo, out var quando) || agora - quando >= RegrasDaContaDoPublico.IntervaloEntreConfirmacoes.TotalSeconds)
                await EnviarConfirmacaoAsync(user, claims.FirstOrDefault(c => c.Type == TiposDeClaim.Nome)?.Value, RegrasDaContaDoPublico.CaminhoDeRetornoSeguro(model.Voltar));
        }
        return CustomResponse(new { mensagem = MensagemDoReenvio });
    }

    private async Task EnviarConfirmacaoAsync(IdentityUser user, string? nome, string? voltar)
    {
        var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var claims = await _userManager.GetClaimsAsync(user);
        foreach (var antiga in claims.Where(c => c.Type == TiposDeClaim.ConfirmacaoEm).ToList())
            await _userManager.RemoveClaimAsync(user, antiga);
        await _userManager.AddClaimAsync(user, new Claim(TiposDeClaim.ConfirmacaoEm, agora));

        var ficha = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = $"{RaizDoSite}/confirmar-email?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(ficha)}" +
                   (voltar is null ? "" : $"&voltar={Uri.EscapeDataString(voltar)}");
        var saudacao = string.IsNullOrWhiteSpace(nome) ? "Olá!" : $"Olá, {nome.Split(' ')[0]}!";
        try
        {
            await _email.EnviarAsync(user.Email!, "Confirme seu e-mail",
                $"{saudacao}\n\nPara ativar sua conta, confirme seu e-mail abrindo o link abaixo (vale por 24 horas e só pode ser usado uma vez):\n\n{link}\n\n" +
                "Se não foi você quem criou a conta, ignore este e-mail: nada será feito sem a confirmação.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao enviar o e-mail de confirmação para {Email}", user.Email);
        }
    }

    /// <summary>Quem tenta criar conta com um e-mail que já tem uma recebe, no lugar do link, o caminho para entrar ou recuperar a senha.</summary>
    private async Task AvisarQueJaTemContaAsync(IdentityUser existente)
    {
        try
        {
            await _email.EnviarAsync(existente.Email!, "Você já tem uma conta",
                $"Recebemos um pedido para criar uma conta com este e-mail, mas ele já está cadastrado.\n\nPara entrar: {RaizDoSite}/entrar\nSe esqueceu a senha: {RaizDoSite}/esqueci-senha\n\n" +
                "Se não foi você quem pediu, ignore este e-mail: nada foi alterado.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao avisar {Email} de cadastro repetido", existente.Email);
        }
    }

    private async Task<bool> Desativado(IdentityUser user) =>
        (await _userManager.GetClaimsAsync(user)).Any(c => c.Type == TiposDeClaim.Desativado && c.Value == "1");
}
