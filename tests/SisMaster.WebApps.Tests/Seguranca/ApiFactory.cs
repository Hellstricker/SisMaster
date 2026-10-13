using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SisMaster.WebApps.Tests.Infra;
using SisMaster.WebApps.WebApi.Data;
using SisMaster.WebApps.WebApi.Email;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// A API de campeonatos inteira no ar, com um banco SQL Server próprio (criado ao subir, apagado ao descartar) e a mesma
/// chave JWT dos tokens que os testes emitem. Os testes de segurança batem nela de verdade, passando por autenticação,
/// autorização por perfil e filtro de associação.
/// </summary>
[CollectionDefinition(ColecaoApi.Nome)]
public sealed class ColecaoApi : ICollectionFixture<ApiFactory>
{
    public const string Nome = "Api";
}

public sealed class CaptadorDeEmail : IEnviadorDeEmail
{
    public List<(string Para, string Assunto, string Corpo)> Enviados { get; } = [];
    public Task EnviarAsync(string para, string assunto, string corpoTexto, CancellationToken ct = default)
    {
        lock (Enviados) Enviados.Add((para, assunto, corpoTexto));
        return Task.CompletedTask;
    }
    public int Quantos(string para, string assuntoComeca) { lock (Enviados) return Enviados.Count(e => e.Para == para && e.Assunto.StartsWith(assuntoComeca)); }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Segredo = "segredo-de-teste-integracao-0123456789abcdef";
    public const string Emissor = "SisMaster";
    public const string Audiencia = "sismaster";

    private readonly string _banco = $"SisMasterTesteApi_{Guid.NewGuid():N}";

    public ApiFactory()
    {
        // O Program lê a configuração logo no início (antes de qualquer hook da fábrica), então ela vai por variável de ambiente.
        // Há uma única fábrica por execução (ver ColecaoApi), então não há disputa entre instâncias.
        Environment.SetEnvironmentVariable("ConnectionStrings__CampeonatoConnection", BancoDeTeste.ComBanco(_banco) + ";MultipleActiveResultSets=true");
        Environment.SetEnvironmentVariable("JwtSettings__Segredo", Segredo);
        Environment.SetEnvironmentVariable("JwtSettings__Emissor", Emissor);
        Environment.SetEnvironmentVariable("JwtSettings__Audiencia", Audiencia);
        Environment.SetEnvironmentVariable("Seguranca__LimiteDeVinculosPorHora", "100000");   // o limite por conta só é testado à parte
    }

    /// <summary>E-mails que a API "enviou" (os testes conferem o aviso de inscrição recebida).</summary>
    public CaptadorDeEmail Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(s => s.AddSingleton<IEnviadorDeEmail>(Emails));
    }

    /// <summary>Token de uma conta do público (Participante): o id da conta e o e-mail (já confirmado) vêm do token.</summary>
    public static string TokenDoParticipante(Guid conta, string email)
    {
        var claims = new List<Claim>
        {
            new("sub", conta.ToString()), new("email", email), new("nome", "Participante"), new("role", "Participante"),
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            Emissor, Audiencia, claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Segredo)), SecurityAlgorithms.HmacSha256)));
    }

    /// <summary>Token válido de um perfil, vinculado às associações informadas (o Administrador não precisa de vínculo).</summary>
    public static string Token(string perfil, params Guid[] associacoes)
    {
        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("email", $"{perfil.ToLowerInvariant()}@teste.local"),
            new("nome", perfil),
            new("role", perfil),
        };
        claims.AddRange(associacoes.Select(a => new Claim("assoc", a.ToString())));

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            Emissor, Audiencia, claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Segredo)), SecurityAlgorithms.HmacSha256)));
    }

    public HttpClient ClienteCom(string? token)
    {
        var cliente = CreateClient();
        if (token is not null) cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                using var escopo = Services.CreateScope();
                escopo.ServiceProvider.GetRequiredService<CampeonatoDbContext>().Database.EnsureDeleted();
            }
            catch { /* o banco de teste é descartável */ }
        }
        base.Dispose(disposing);
    }
}
