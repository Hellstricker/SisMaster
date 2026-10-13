using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SisMaster.Identidade.Api.Models;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApi.Core.Identities;

namespace SisMaster.Identidade.Api.Services;

/// <summary>Monta a resposta de login (token JWT e dados do usuário). Usado pelo login, pela troca de senha e pela confirmação de e-mail.</summary>
public class EmissorDeToken
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly JwtSettings _jwtSettings;

    public EmissorDeToken(UserManager<IdentityUser> userManager, IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<UsuarioRespostaLogin> GerarAsync(IdentityUser user)
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

        // Árbitro: sessão longa (cobre um jogo inteiro). Administradores e participantes: curta, renovada enquanto usam o sistema.
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
                TrocarSenha = trocarSenha,
                TermosVersao = claimsDoUsuario.FirstOrDefault(c => c.Type == TiposDeClaim.TermosVersao)?.Value
            }
        };
    }
}
