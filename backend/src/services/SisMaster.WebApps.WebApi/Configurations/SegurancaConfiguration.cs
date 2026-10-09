using Microsoft.AspNetCore.Mvc;
using SisMaster.WebApi.Core.Autorizacao;
using SisMaster.WebApi.Core.Configurations;
using SisMaster.WebApps.WebApi.Seguranca;

namespace SisMaster.WebApps.WebApi.Configurations;

public static class SegurancaConfiguration
{
    /// <summary>
    /// Autenticação (token JWT emitido pela Identidade), autorização por perfil e o filtro que confere a associação do recurso.
    /// Todo endpoint passa a exigir usuário autenticado; o que for público precisa de [AllowAnonymous] explícito.
    /// </summary>
    public static IServiceCollection AddSegurancaConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddJwtConfiguration(configuration);
        services.AddAutorizacaoSisMaster();
        services.AddScoped<IResolvedorDeAssociacao, ResolvedorDeAssociacao>();
        services.AddScoped<EscopoDeAssociacaoFilter>();
        services.Configure<MvcOptions>(o => o.Filters.AddService<EscopoDeAssociacaoFilter>());
        return services;
    }
}
