using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SisMaster.Identidade.Api.Configurations;
using SisMaster.Identidade.Api.Data;
using SisMaster.Identidade.Api.Extensions;
using SisMaster.WebApi.Core.Configurations;
using System.Text;

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

app
    .UseSwaggerConfiguration()
    .UseApiConfiguration()
    .MigrateDatabaseAsync().GetAwaiter().GetResult();

app.Run();
