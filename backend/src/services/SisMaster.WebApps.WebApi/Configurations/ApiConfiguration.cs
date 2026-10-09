using System.Text.Json.Serialization;
using SisMaster.WebApi.Core.Configurations;
using SisMaster.WebApps.WebApi.Hubs;

namespace SisMaster.WebApps.WebApi.Configurations;

public static class ApiConfiguration
{
    public static IServiceCollection AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSignalR();
        // Tudo roda na mesma origem (o nginx serve o front e encaminha /api); CORS só se houver origens configuradas (Cors:Origens).
        var origens = configuration.GetSection("Cors:Origens").Get<string[]>() ?? [];
        if (origens.Length > 0)
            services.AddCors(options => options.AddPolicy("CorsPolicy", builder =>
                builder.WithOrigins(origens).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));
        return services;
    }

    public static WebApplication UseApiConfiguration(this WebApplication app)
    {
        if (app.Configuration.GetSection("Cors:Origens").Get<string[]>() is { Length: > 0 })
            app.UseCors("CorsPolicy");
        app.UseAuthConfiguration();
        app.MapControllers();
        app.MapHub<SumulaHub>("/hubs/sumula");
        return app;
    }
}
