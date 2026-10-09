using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SisMaster.Identidade.Api.Models;
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

    public AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, IOptions<JwtSettings> jwtSettings)
    {
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

        return CustomResponse(await GerarResposta(user));
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

    private async Task<UsuarioRespostaLogin> GerarResposta(IdentityUser user)
    {
        var claimsDoUsuario = await _userManager.GetClaimsAsync(user);
        var perfis = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(TiposDeClaim.Nome, claimsDoUsuario.FirstOrDefault(c => c.Type == TiposDeClaim.Nome)?.Value ?? user.Email!),
        };
        claims.AddRange(perfis.Select(p => new Claim(TiposDeClaim.Papel, p)));
        // O Administrador acessa todas as associações; os outros levam no token só as vinculadas.
        if (!perfis.Contains(Perfis.Administrador))
            claims.AddRange(claimsDoUsuario.Where(c => c.Type == TiposDeClaim.Associacao).Select(c => new Claim(TiposDeClaim.Associacao, c.Value)));
        var trocarSenha = claimsDoUsuario.Any(c => c.Type == TiposDeClaim.TrocarSenha && c.Value == "1");
        if (trocarSenha) claims.Add(new Claim(TiposDeClaim.TrocarSenha, "1"));

        // Árbitro: sessão longa (cobre um jogo inteiro). Administradores: curta, renovada enquanto usam o sistema.
        var duracao = perfis.Contains(Perfis.Arbitro)
            ? TimeSpan.FromHours(_jwtSettings.HorasParaExpirarArbitro)
            : TimeSpan.FromMinutes(_jwtSettings.MinutosParaExpirar);

        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: _jwtSettings.Emissor,
            audience: _jwtSettings.Audiencia,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(duracao),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Segredo!)), SecurityAlgorithms.HmacSha256)));

        return new UsuarioRespostaLogin
        {
            AccessToken = token,
            ExpiresIn = duracao.TotalSeconds,
            UserToken = new UsuarioToken
            {
                Id = user.Id,
                Email = user.Email,
                Nome = claims.First(c => c.Type == TiposDeClaim.Nome).Value,
                Perfil = perfis.FirstOrDefault(),
                Associacoes = claims.Where(c => c.Type == TiposDeClaim.Associacao).Select(c => c.Value).ToList(),
                TrocarSenha = trocarSenha
            }
        };
    }
}
