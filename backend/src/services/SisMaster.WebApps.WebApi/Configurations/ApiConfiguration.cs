using SisMaster.WebApps.WebApi.Hubs;

namespace SisMaster.WebApps.WebApi.Configurations;

public static class ApiConfiguration
{
    public static IServiceCollection AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddSignalR();
        services.AddCors(options =>
        {
            options.AddPolicy("CorsPolicy", builder =>
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader());
        });
        return services;
    }

    public static WebApplication UseApiConfiguration(this WebApplication app)
    {
        app.UseCors("CorsPolicy");
        app.MapControllers();
        app.MapHub<SumulaHub>("/hubs/sumula");
        return app;
    }
}
