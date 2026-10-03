using Microsoft.OpenApi;
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
            app.UseHttpsRedirection();
            app.UseAuthConfiguration();
            app.MapControllers();

            return app;
        }
    }
}
