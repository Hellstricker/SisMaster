using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SisMaster.Identidade.Api.Email;
using SisMaster.Identidade.Api.Models;
using SisMaster.Identidade.Api.Services;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApi.Core.Controllers;
using SisMaster.WebApi.Core.Identities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SisMaster.Identidade.Api.Controllers;

/// <summary>
/// Entrada no sistema. Não existe cadastro aberto: as contas são criadas por um administrador (gestão de usuários)
/// ou pelo administrador inicial definido na configuração (Bootstrap).
/// </summary>
[Route("api/identidade")]
public class AuthController : MainController
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly JwtSettings _jwtSettings;
    private readonly IEnviadorDeEmail _email;
    private readonly ILogger<AuthController> _log;
    private readonly IConfiguration _configuracao;
    private readonly EmissorDeToken _emissor;

    public AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, IOptions<JwtSettings> jwtSettings,
        IEnviadorDeEmail email, ILogger<AuthController> log, IConfiguration configuracao, EmissorDeToken emissor)
    {
        _emissor = emissor;
        _email = email;
        _log = log;
        _configuracao = configuracao;
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtSettings = jwtSettings.Value;
    }

    /// <summary>Entra com e-mail e senha. Cinco erros seguidos bloqueiam a conta por 15 minutos.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("autenticar")]
    public async Task<IActionResult> Autenticar([FromBody] UsuarioLogin model)
    {
        if (!ModelState.IsValid) return CustomResponse(ModelState);

        var user = await _userManager.FindByEmailAsync(model.Email!);

        // Mensagem única para "não existe", "senha errada" e "desativada": não revela quais e-mails têm conta.
        if (user is null)
        {
            AdicionarErro("E-mail ou senha incorretos.");
            return CustomResponse();
        }

        if (await Desativado(user))
        {
            AdicionarErro("Esta conta está desativada. Fale com um administrador.");
            return CustomResponse();
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password!, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            AdicionarErro("Acesso bloqueado por tentativas demais. Tente de novo em alguns minutos ou peça a um administrador para liberar.");
            return CustomResponse();
        }
        if (!result.Succeeded)
        {
            AdicionarErro("E-mail ou senha incorretos.");
            return CustomResponse();
        }

        // Conta do público: só entra depois de confirmar o e-mail (a senha já estava certa, então não revela nada a estranhos).
        if (!user.EmailConfirmed && await _userManager.IsInRoleAsync(user, Perfis.Participante))
        {
            AdicionarErro(MensagemEmailNaoConfirmado);
            return CustomResponse();
        }

        return CustomResponse(await GerarResposta(user));
    }

    /// <summary>Mensagem do login de conta do público que ainda não confirmou o e-mail (a tela a reconhece para oferecer o reenvio).</summary>
    public const string MensagemEmailNaoConfirmado = "Você ainda não confirmou o e-mail desta conta. Abra o link que enviamos ou peça um novo.";

    /// <summary>
    /// "Esqueci minha senha": envia ao e-mail da conta um link de uso único (válido por 1 hora) para definir uma nova senha.
    /// A senha atual NÃO é alterada: quem só conhece o e-mail de outra pessoa não consegue tirá-la do sistema.
    /// A resposta é sempre a mesma, exista ou não a conta, e há um intervalo mínimo entre pedidos da mesma conta,
    /// para ninguém descobrir e-mails cadastrados nem lotar a caixa de outra pessoa.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("esqueci-senha")]
    public async Task<IActionResult> EsqueciSenha([FromBody] EsqueciSenha model)
    {
        if (!ModelState.IsValid) return CustomResponse(ModelState);

        var user = await _userManager.FindByEmailAsync(model.Email!.Trim());
        if (user is not null && !await Desativado(user))
        {
            var claims = await _userManager.GetClaimsAsync(user);
            var ultimo = claims.FirstOrDefault(c => c.Type == TiposDeClaim.RecuperacaoEm)?.Value;
            var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var liberado = !long.TryParse(ultimo, out var quando) || agora - quando >= IntervaloEntreRecuperacoes.TotalSeconds;
            if (liberado) await RecuperarAsync(user, claims, agora);
        }

        return CustomResponse(new { mensagem = "Se o e-mail estiver cadastrado, enviamos um link para definir uma nova senha." });
    }

    /// <summary>
    /// Conclui a recuperação: com o link recebido por e-mail (e-mail + token) a pessoa define a nova senha.
    /// Libera conta bloqueada e tira a marca de senha temporária. Link inválido, vencido ou já usado dá sempre a mesma mensagem.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("redefinir-senha")]
    public async Task<IActionResult> RedefinirSenha([FromBody] RedefinirSenha model)
    {
        if (!ModelState.IsValid) return CustomResponse(ModelState);

        const string linkInvalido = "Este link é inválido ou já venceu. Peça um novo em \"Esqueci minha senha\".";
        var user = await _userManager.FindByEmailAsync(model.Email!.Trim());
        if (user is null || await Desativado(user))
        {
            AdicionarErro(linkInvalido);
            return CustomResponse();
        }

        var resultado = await _userManager.ResetPasswordAsync(user, model.Token!, model.NovaSenha!);
        if (!resultado.Succeeded)
        {
            // Erros da regra de senha (tamanho, letra e número) a pessoa precisa ler; os de token viram a mensagem única.
            var daSenha = resultado.Errors.Where(e => !e.Code.StartsWith("InvalidToken")).Select(e => e.Description).ToArray();
            if (daSenha.Length > 0) AdicionarErros(daSenha); else AdicionarErro(linkInvalido);
            return CustomResponse();
        }

        foreach (var marca in (await _userManager.GetClaimsAsync(user)).Where(c => c.Type is TiposDeClaim.TrocarSenha or TiposDeClaim.RecuperacaoEm).ToList())
            await _userManager.RemoveClaimAsync(user, marca);
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        // Quem abriu o link recebido no e-mail provou que controla esse e-mail: a conta do público fica confirmada.
        if (!user.EmailConfirmed && await _userManager.IsInRoleAsync(user, Perfis.Participante))
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
        }

        return CustomResponse(new { mensagem = "Senha alterada. Entre com a nova senha." });
    }

    /// <summary>Intervalo mínimo entre dois pedidos de recuperação da mesma conta.</summary>
    public static readonly TimeSpan IntervaloEntreRecuperacoes = TimeSpan.FromMinutes(10);

    private async Task RecuperarAsync(IdentityUser user, IList<Claim> claims, long agora)
    {
        var ficha = await _userManager.GeneratePasswordResetTokenAsync(user);
        foreach (var antiga in claims.Where(c => c.Type == TiposDeClaim.RecuperacaoEm).ToList())
            await _userManager.RemoveClaimAsync(user, antiga);
        await _userManager.AddClaimAsync(user, new Claim(TiposDeClaim.RecuperacaoEm, agora.ToString()));

        var raiz = (_configuracao["App:UrlPublica"] ?? "http://localhost:3000").TrimEnd('/');
        var area = await _userManager.IsInRoleAsync(user, Perfis.Participante) ? "" : "/admin";   // participante usa o site; a equipe, a gestão
        var link = $"{raiz}{area}/redefinir-senha?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(ficha)}";

        try
        {
            await _email.EnviarAsync(user.Email!, "Recuperação de senha do SisMaster",
                $"Recebemos um pedido para redefinir a senha de {user.Email}.\n\nPara definir uma nova senha, abra o link abaixo (vale por 1 hora e só pode ser usado uma vez):\n\n{link}\n\n" +
                "Se não foi você quem pediu, ignore este e-mail: a sua senha atual continua valendo e nada foi alterado.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao enviar o e-mail de recuperação de senha para {Email}", user.Email);
        }
    }

    /// <summary>Renova o token de quem já está autenticado (o front chama enquanto o administrador usa o sistema).</summary>
    [Authorize]
    [HttpPost("renovar")]
    public async Task<IActionResult> Renovar()
    {
        var id = User.FindFirstValue("sub");
        var user = id is null ? null : await _userManager.FindByIdAsync(id);
        if (user is null || await Desativado(user) || await _userManager.IsLockedOutAsync(user))
            return Unauthorized();

        return CustomResponse(await GerarResposta(user));
    }

    /// <summary>
    /// A própria pessoa troca a senha (exige a atual). Tira a marca de "senha temporária" e devolve um token novo, sem a marca.
    /// </summary>
    [Authorize]
    [HttpPost("trocar-senha")]
    public async Task<IActionResult> TrocarSenha([FromBody] TrocarSenha model)
    {
        if (!ModelState.IsValid) return CustomResponse(ModelState);

        var id = User.FindFirstValue("sub");
        var user = id is null ? null : await _userManager.FindByIdAsync(id);
        if (user is null || await Desativado(user)) return Unauthorized();

        if (model.NovaSenha == model.SenhaAtual)
        {
            AdicionarErro("A nova senha deve ser diferente da atual.");
            return CustomResponse();
        }

        var resultado = await _userManager.ChangePasswordAsync(user, model.SenhaAtual!, model.NovaSenha!);
        if (!resultado.Succeeded)
        {
            AdicionarErros(resultado.Errors.Select(e => e.Code == "PasswordMismatch" ? "Senha atual incorreta." : e.Description).ToArray());
            return CustomResponse();
        }

        foreach (var marca in (await _userManager.GetClaimsAsync(user)).Where(c => c.Type == TiposDeClaim.TrocarSenha).ToList())
            await _userManager.RemoveClaimAsync(user, marca);

        return CustomResponse(await GerarResposta(user));
    }

    private async Task<bool> Desativado(IdentityUser user) =>
        (await _userManager.GetClaimsAsync(user)).Any(c => c.Type == TiposDeClaim.Desativado && c.Value == "1");

    private Task<UsuarioRespostaLogin> GerarResposta(IdentityUser user) => _emissor.GerarAsync(user);
}
