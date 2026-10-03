using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SisMaster.Identidade.Api.Models;
using SisMaster.WebApi.Core.Controllers;
using SisMaster.WebApi.Core.Identities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SisMaster.Identidade.Api.Controllers;

[Route("api/identidade")]
public class AuthController : MainController
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly JwtSettings _jwtSettings;


    public AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, RoleManager<IdentityRole> roleManager, IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _jwtSettings = jwtSettings.Value;
    }

    [HttpPost("nova-conta")]
    public async Task<IActionResult> Registrar([FromBody] UsuarioRegistro model)
    {
        if (!ModelState.IsValid) return CustomResponse(ModelState);

        var user = new IdentityUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password!);

        if (result.Succeeded)
        {
            var roleExiste = await _roleManager.RoleExistsAsync(nameof(model.Perfil));
            if (!roleExiste)
            {
              var roleResult = await _roleManager.CreateAsync(new IdentityRole(nameof(model.Perfil)));
                if(!roleResult.Succeeded)
                {
                    AdicionarErros(roleResult.Errors.Select(e => e.Description).ToArray());
                    await _userManager.DeleteAsync(user);
                    return CustomResponse();
                }
            }
            await _userManager.AddToRoleAsync(user, nameof(model.Perfil));
            return CustomResponse(await GetJwt(model.Email!));
        }

        AdicionarErros(result.Errors.Select(e => e.Description).ToArray());
        return CustomResponse();
    }

    [HttpPost("autenticar")]
    public async Task<IActionResult> Autenticar([FromBody] UsuarioLogin model)
    {
        if (!ModelState.IsValid) return CustomResponse(ModelState);

        var result = await _signInManager.PasswordSignInAsync(model.Email!, model.Password!, isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
            return CustomResponse(await GetJwt(model.Email!));

        if (result.IsLockedOut)
            AdicionarErro("Usuário bloqueado");
        else
            AdicionarErro("Usuário ou senha inválidos");

        return CustomResponse();
    }

    private async Task<UsuarioRespostaLogin> GetJwt(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        var claims = await _userManager.GetClaimsAsync(user!);

        var identityClaims = await ObterClaimsDoUsuario(claims, user!);
        var encodedToken = CodificarToken(identityClaims);

        return ObterRespostaToken(encodedToken, user!, claims);
    }

    private async Task<ClaimsIdentity> ObterClaimsDoUsuario(ICollection<Claim> claims, IdentityUser user)
    {
        var roles = await _userManager.GetRolesAsync(user!);

        claims.Add(new Claim(JwtRegisteredClaimNames.Sub, user!.Id.ToString()));
        claims.Add(new Claim(JwtRegisteredClaimNames.Email, user!.Email!));
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
        claims.Add(new Claim(JwtRegisteredClaimNames.Nbf, ToUnixEpocheDate(DateTime.UtcNow).ToString()));
        claims.Add(new Claim(JwtRegisteredClaimNames.Iat, ToUnixEpocheDate(DateTime.Now).ToString(), ClaimValueTypes.Integer64));
        claims.Add(new Claim("nome", user.UserName!));
        claims.Add(new Claim("id", user.Id!));

        foreach (var role in roles)
            claims.Add(new Claim("role", role));

        return new ClaimsIdentity(claims);
    }

    private string CodificarToken(ClaimsIdentity identityClaims)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSettings.Segredo!);

        var token = tokenHandler.CreateToken(new SecurityTokenDescriptor()
        {
            Subject = identityClaims,
            Issuer = _jwtSettings.Emissor,
            Audience = _jwtSettings.Audiencia,
            Expires = DateTime.UtcNow.AddHours(_jwtSettings.HorasParaExpirar),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        });

        return tokenHandler.WriteToken(token);
    }

    private UsuarioRespostaLogin ObterRespostaToken(string encodedToken, IdentityUser user, IEnumerable<Claim> claims)
    {
        return new UsuarioRespostaLogin()
        {
            AccessToken = encodedToken,
            ExpiresIn = TimeSpan.FromHours(_jwtSettings.HorasParaExpirar).TotalSeconds,
            UserToken = new UsuarioToken()
            {
                Id = user.Id.ToString(),
                Email = user.Email,
                Claims = claims.Select(c => new UsuarioClaim() { Type = c.Type, Value = c.Value })
            }
        };
    }

    private static long ToUnixEpocheDate(DateTime date)
    {
        return (long)Math.Round((date.ToUniversalTime() - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalSeconds);
    }
}