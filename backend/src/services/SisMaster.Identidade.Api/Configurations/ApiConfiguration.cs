using SisMaster.WebApi.Core.Configurations;

namespace SisMaster.Identidade.Api.Configurations
{
    public static class ApiConfiguration
    {
        public static IServiceCollection AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllers();
            return services;
        }

        public static WebApplication UseApiConfiguration(this WebApplication app)
        {
            // O HTTPS é do nginx na frente; o container só fala HTTP dentro da rede do Docker.
            app.UseRateLimiter();
            app.UseAuthConfiguration();
            app.MapControllers();

            return app;
        }
    }
}
