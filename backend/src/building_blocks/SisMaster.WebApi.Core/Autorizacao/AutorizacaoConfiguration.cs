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
            // Padrão para toda rota sem política própria: só a EQUIPE. Assim a conta do público (Participante) nunca alcança,
            // por esquecimento, uma rota da gestão; ela só usa as rotas que declaram a política Participante.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireRole(Perfis.Equipe).Build())
            .AddPolicy(Politicas.Gestao, p => p.RequireRole(Perfis.Administrador, Perfis.AdministradorDeAssociacao))
            .AddPolicy(Politicas.Arbitragem, p => p.RequireRole(Perfis.Administrador, Perfis.AdministradorDeAssociacao, Perfis.Arbitro))
            .AddPolicy(Politicas.SomenteAdministrador, p => p.RequireRole(Perfis.Administrador))
            .AddPolicy(Politicas.Participante, p => p.RequireRole(Perfis.Participante));
        return services;
    }
}
