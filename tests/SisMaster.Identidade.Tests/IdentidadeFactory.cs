using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SisMaster.Identidade.Api.Data;
using SisMaster.WebApps.Tests.Infra;
using Xunit;

namespace SisMaster.Identidade.Tests;

[CollectionDefinition(ColecaoIdentidade.Nome)]
public sealed class ColecaoIdentidade : ICollectionFixture<IdentidadeFactory>
{
    public const string Nome = "Identidade";
}

/// <summary>A Identidade inteira no ar, com banco SQL Server próprio (criado ao subir, apagado ao descartar).</summary>
public sealed class IdentidadeFactory : WebApplicationFactory<Program>
{
    public const string Segredo = "segredo-de-teste-da-identidade-0123456789abcdef";
    public const string AdminEmail = "admin@teste.local";
    public const string AdminSenha = "Teste1234";

    private readonly string _banco = $"SisMasterTesteIdentidade_{Guid.NewGuid():N}";

    public IdentidadeFactory()
    {
        // O Program lê a configuração logo no início, então ela vai por variável de ambiente (há uma única fábrica por execução).
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", BancoDeTeste.ComBanco(_banco) + ";MultipleActiveResultSets=true");
        Environment.SetEnvironmentVariable("JwtSettings__Segredo", Segredo);
        Environment.SetEnvironmentVariable("JwtSettings__Emissor", "SisMaster");
        Environment.SetEnvironmentVariable("JwtSettings__Audiencia", "sismaster");
        Environment.SetEnvironmentVariable("JwtSettings__MinutosParaExpirar", "30");
        Environment.SetEnvironmentVariable("JwtSettings__HorasParaExpirarArbitro", "8");
        Environment.SetEnvironmentVariable("Seguranca__LimiteDeLoginsPorMinuto", "100000"); // o limite por endereço atrapalharia os testes
        Environment.SetEnvironmentVariable("Seguranca__LimiteDeCadastrosPorHora", "100000");
        Environment.SetEnvironmentVariable("Bootstrap__AdminEmail", AdminEmail);
        Environment.SetEnvironmentVariable("Bootstrap__AdminSenha", AdminSenha);
        Environment.SetEnvironmentVariable("Bootstrap__AdminNome", "Admin Teste");
    }

    /// <summary>E-mails que a API "enviou" (o teste lê a senha temporária daqui).</summary>
    public CaptadorDeEmail Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(s => s.AddSingleton<SisMaster.Identidade.Api.Email.IEnviadorDeEmail>(Emails));
    }

    /// <summary>Cria uma conta como o gerenciador de usuários (a gestão de usuários chega no S2).</summary>
    public async Task<string> CriarUsuarioAsync(string perfil, string? senha = null, string? nome = null, IEnumerable<Guid>? associacoes = null, bool desativado = false)
    {
        var email = $"{perfil.ToLowerInvariant()}.{Guid.NewGuid():N}@teste.local";
        using var escopo = Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var resultado = await usuarios.CreateAsync(usuario, senha ?? "Senha1234");
        if (!resultado.Succeeded) throw new InvalidOperationException(string.Join("; ", resultado.Errors.Select(e => e.Description)));
        await usuarios.AddToRoleAsync(usuario, perfil);

        var claims = new List<System.Security.Claims.Claim> { new("nome", nome ?? perfil) };
        claims.AddRange((associacoes ?? []).Select(a => new System.Security.Claims.Claim("assoc", a.ToString())));
        if (desativado) claims.Add(new("desativado", "1"));
        await usuarios.AddClaimsAsync(usuario, claims);
        return email;
    }

    public static JwtSecurityToken Ler(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                using var escopo = Services.CreateScope();
                escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureDeleted();
            }
            catch { /* o banco de teste é descartável */ }
        }
        base.Dispose(disposing);
    }
}
