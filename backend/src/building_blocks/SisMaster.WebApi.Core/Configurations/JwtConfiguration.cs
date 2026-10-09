using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApi.Core.Identities;
using System.Text;

namespace SisMaster.WebApi.Core.Configurations
{
    public static class JwtConfiguration
    {
        public static IServiceCollection AddJwtConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtSecretKey = configuration["JwtSettings:Segredo"];
            if (string.IsNullOrWhiteSpace(jwtSecretKey) || jwtSecretKey.Length < 32)
                throw new InvalidOperationException(
                    "Chave secreta JWT não configurada (mínimo 32 caracteres). Defina JwtSettings__Segredo (no Docker ela vem de JWT_SEGREDO no .env).");

            var appSettingSection = configuration.GetSection(nameof(JwtSettings));
            services.Configure<JwtSettings>(appSettingSection);

            var appsSettings = appSettingSection.Get<JwtSettings>();
            var key = Encoding.UTF8.GetBytes(appsSettings!.Segredo!);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // chave simétrica, sem metadata remota; o HTTPS é do nginx na frente
                options.SaveToken = true;
                options.MapInboundClaims = false;     // mantém os nomes do token (sub, role, assoc, nome...)
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = appsSettings.Emissor,
                    ValidateAudience = true,
                    ValidAudience = appsSettings.Audiencia,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = TiposDeClaim.Papel,
                    NameClaimType = TiposDeClaim.Nome
                };
                // O WebSocket do SignalR não manda o cabeçalho Authorization: o token vai na query (access_token), só nas rotas /hubs.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });
            return services;
        }

        public static WebApplication UseAuthConfiguration(this WebApplication app)
        {
            app
                .UseAuthentication()
                .UseAuthorization();
            return app;
        }
    }
}
