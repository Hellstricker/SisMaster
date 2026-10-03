namespace SisMaster.WebApps.WebApi.Configurations;

public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services
            .AddEndpointsApiExplorer()
            .AddSwaggerGen();

        return services;
    }

    public static WebApplication UseSwaggerConfiguration(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SisMaster API v1");
                c.RoutePrefix = string.Empty;
            });
        }
        return app;
    }
}
