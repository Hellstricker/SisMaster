using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using SisMaster.WebApi.Core.Configurations;
using SisMaster.WebApps.WebApi.Email;
using SisMaster.WebApps.WebApi.Hubs;

namespace SisMaster.WebApps.WebApi.Configurations;

public static class ApiConfiguration
{
    public static IServiceCollection AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddEmail(configuration);
        // Ligar a conta a um cadastro pelo CPF: poucas tentativas por conta (cada CPF recusado revelaria que ele já existe na base).
        var tentativasDeVinculo = configuration.GetValue("Seguranca:LimiteDeVinculosPorHora", 8);
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("vinculo", contexto => RateLimitPartition.GetFixedWindowLimiter(
                contexto.User.FindFirst("sub")?.Value ?? contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = contexto.RequestServices.GetRequiredService<IConfiguration>().GetValue("Seguranca:LimiteDeVinculosPorHora", tentativasDeVinculo),
                    Window = TimeSpan.FromHours(1), QueueLimit = 0
                }));
        });
        // Batimento de 5 s e tolerância de 20 s: a queda de rede na quadra é percebida rápido (o padrão é 15 s / 30 s).
        services.AddSignalR(o =>
        {
            o.KeepAliveInterval = TimeSpan.FromSeconds(5);
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(20);
        });
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
        app.UseRateLimiter();   // depois da autenticação: o limite do vínculo é por conta
        app.MapControllers();
        // O placar público conecta sem login (só recebe). A conexão é liberada aqui; cada método de escrita do hub exige
        // o perfil de arbitragem por [Authorize] e a associação do jogo (ver SumulaHub).
        app.MapHub<SumulaHub>("/hubs/sumula").AllowAnonymous();
        return app;
    }
}
