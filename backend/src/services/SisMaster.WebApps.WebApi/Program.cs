using Microsoft.EntityFrameworkCore;
using SisMaster.WebApps.WebApi.Configurations;
using SisMaster.WebApps.WebApi.Data;

var builder = WebApplication.CreateBuilder(args);

var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile($"appsettings.{environment}.json", true, true)
    .AddEnvironmentVariables();

builder.Services
    .AddApiConfiguration(builder.Configuration)
    .AddSwaggerConfiguration()
    .AddDependenciesConfiguration(builder.Configuration);

var app = builder.Build();

// Aplica migrations pendentes automaticamente na inicialização
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CampeonatoDbContext>();
    db.Database.Migrate();
}

app.UseSwaggerConfiguration()
   .UseApiConfiguration();

app.Run();
