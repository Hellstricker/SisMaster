using Microsoft.AspNetCore.HttpOverrides;
using SisMaster.Identidade.Api.Configurations;
using SisMaster.WebApi.Core.Configurations;

var builder = WebApplication.CreateBuilder(args);

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile($"appsettings.{environment}.json", true, true)
    .AddEnvironmentVariables();

builder.Services
    .AddIdentityConfiguration(builder.Configuration)
    .AddApiConfiguration(builder.Configuration)
    .AddSwaggerConfiguration();

var app = builder.Build();

await app.MigrateDatabaseAsync();
await app.GarantirPerfisEAdministradorAsync();

app.UseForwardedHeaders();
app.UseSwaggerConfiguration()
   .UseApiConfiguration();

app.Run();

/// <summary>Necessário para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
