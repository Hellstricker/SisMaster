using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SisMaster.WebApi.Core.Usuarios;

namespace SisMaster.WebApi.Core.Autorizacao;

public static class AutorizacaoConfiguration
{
    /// <summary>
    /// Registra as políticas e torna o usuário autenticado o padrão de TODO endpoint (FallbackPolicy):
    /// o que for público precisa de [AllowAnonymous] explícito.
    /// </summary>
    public static IServiceCollection AddAutorizacaoSisMaster(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAcessoDoUsuario, AcessoDoUsuario>();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Politicas.Gestao, p => p.RequireRole(Perfis.Administrador, Perfis.AdministradorDeAssociacao))
            .AddPolicy(Politicas.Arbitragem, p => p.RequireRole(Perfis.Administrador, Perfis.AdministradorDeAssociacao, Perfis.Arbitro))
            .AddPolicy(Politicas.SomenteAdministrador, p => p.RequireRole(Perfis.Administrador));
        return services;
    }
}
