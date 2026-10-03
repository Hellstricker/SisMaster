namespace SisMaster.Identidade.Api.Configurations
{
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
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SisMaster Identidade API v1");
                    c.RoutePrefix = string.Empty; // Para acessar diretamente na raiz
                });
            }
            return app;
        }
    }
}
