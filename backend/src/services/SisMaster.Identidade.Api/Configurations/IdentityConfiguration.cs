using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SisMaster.Identidade.Api.Data;
using SisMaster.Identidade.Api.Extensions;
using SisMaster.WebApi.Core.Configurations;

namespace SisMaster.Identidade.Api.Configurations
{
    public static class IdentityConfiguration
    {
        public static IServiceCollection AddIdentityConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            var sqlConnectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(sqlConnectionString))
                throw new InvalidOperationException("Base de dados não configurada no appsettings.json");

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(sqlConnectionString));

            services.AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;                
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddErrorDescriber<IdentityPortugueseMessages>()
            .AddDefaultTokenProviders();

            services.AddJwtConfiguration(configuration);
            return services;
        }
    }
}
