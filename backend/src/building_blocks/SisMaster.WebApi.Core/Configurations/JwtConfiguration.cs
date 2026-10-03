using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SisMaster.WebApi.Core.Identities;
using System.Text;

namespace SisMaster.WebApi.Core.Configurations
{
    public static class JwtConfiguration
    {
        public static IServiceCollection AddJwtConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtSecretKey = configuration["JwtSettings:Segredo"];
            if (string.IsNullOrEmpty(jwtSecretKey))
                throw new InvalidOperationException("Chave secreta JWT não configurada no appsettings.json");

            var appSettingSection = configuration.GetSection(nameof(JwtSettings));
            services.Configure<JwtSettings>(appSettingSection);

            var appsSettings = appSettingSection.Get<JwtSettings>();
            var key = Encoding.ASCII.GetBytes(appsSettings!.Segredo!);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = true;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = appsSettings!.Audiencia,
                    ValidIssuer = appsSettings.Emissor
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
