using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SisMaster.Identidade.Api.Data;
using SisMaster.Identidade.Api.Email;
using SisMaster.Identidade.Api.Extensions;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApi.Core.Configurations;

namespace SisMaster.Identidade.Api.Configurations
{
    public static class IdentityConfiguration
    {
        public static IServiceCollection AddIdentityConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            var sqlConnectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(sqlConnectionString))
                throw new InvalidOperationException(
                    "Base de dados da Identidade não configurada. Defina ConnectionStrings__DefaultConnection (no Docker ela vem do .env).");

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(sqlConnectionString));

            services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                // Senha: pelo menos 8 caracteres, com letra e número (a letra é conferida pelo validador abaixo).
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.User.RequireUniqueEmail = true;

                // 5 erros seguidos bloqueiam a conta por 15 minutos (um administrador pode liberar).
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddErrorDescriber<IdentityPortugueseMessages>()
            .AddPasswordValidator<SenhaComLetraValidator>()
            .AddDefaultTokenProviders();

            // Link de recuperação de senha: vale por 1 hora.
            services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));

            services.AddJwtConfiguration(configuration);
            services.AddEmail(configuration);
            services.AddAutorizacaoSisMaster();

            // Limite de tentativas de login por endereço (além do bloqueio por conta). Configurável: Seguranca:LimiteDeLoginsPorMinuto.
            var limiteDeLogins = configuration.GetValue("Seguranca:LimiteDeLoginsPorMinuto", 20);
            services.AddRateLimiter(o =>
            {
                o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                o.AddPolicy("login", contexto => RateLimitPartition.GetFixedWindowLimiter(
                    contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteDeLogins, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            });

            // Atrás do nginx: o endereço real do cliente vem em X-Forwarded-For (o container só é alcançável pela rede do Docker).
            services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                o.KnownIPNetworks.Clear();
                o.KnownProxies.Clear();
            });
            return services;
        }
    }

    /// <summary>Exige pelo menos uma letra (o Identity só sabe exigir maiúscula ou minúscula).</summary>
    public sealed class SenhaComLetraValidator : IPasswordValidator<IdentityUser>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user, string? password) =>
            Task.FromResult(password is not null && password.Any(char.IsLetter)
                ? IdentityResult.Success
                : IdentityResult.Failed(new IdentityError { Code = "PasswordRequiresLetter", Description = "A senha deve conter pelo menos uma letra." }));
    }
}
