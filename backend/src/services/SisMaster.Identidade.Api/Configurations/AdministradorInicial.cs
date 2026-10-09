using Microsoft.AspNetCore.Identity;
using SisMaster.Core.Enums;
using SisMaster.WebApi.Core.Autorizacao;

namespace SisMaster.Identidade.Api.Configurations;

public static class AdministradorInicial
{
    /// <summary>
    /// Garante os três perfis (papéis) e, se ainda não existe NENHUM usuário, cria o administrador inicial a partir da configuração
    /// (Bootstrap:AdminEmail, Bootstrap:AdminSenha e, opcionalmente, Bootstrap:AdminNome). Sem usuários e sem essa configuração,
    /// ninguém consegue entrar: o aviso no log diz o que definir. A conta nasce com senha temporária (troca obrigatória).
    /// </summary>
    public static Task GarantirPerfisEAdministradorAsync(this WebApplication app) => GarantirAsync(app.Services, app.Configuration);

    public static async Task GarantirAsync(IServiceProvider servicos, IConfiguration configuracao)
    {
        using var escopo = servicos.CreateScope();
        var roles = escopo.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var log = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdministradorInicial");

        foreach (var perfil in Enum.GetNames<PerfilUsuario>())
            if (!await roles.RoleExistsAsync(perfil))
                await roles.CreateAsync(new IdentityRole(perfil));

        if (usuarios.Users.Any()) return;

        var email = configuracao["Bootstrap:AdminEmail"];
        var senha = configuracao["Bootstrap:AdminSenha"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            log.LogWarning("Não há usuários e o administrador inicial não está configurado. Defina Bootstrap__AdminEmail e Bootstrap__AdminSenha (no Docker: ADMIN_EMAIL e ADMIN_SENHA_INICIAL no .env).");
            return;
        }

        var admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var resultado = await usuarios.CreateAsync(admin, senha);
        if (!resultado.Succeeded)
        {
            log.LogError("Não foi possível criar o administrador inicial: {Erros}", string.Join("; ", resultado.Errors.Select(e => e.Description)));
            return;
        }
        await usuarios.AddToRoleAsync(admin, Perfis.Administrador);
        await usuarios.AddClaimsAsync(admin,
        [
            new(TiposDeClaim.Nome, configuracao["Bootstrap:AdminNome"] ?? "Administrador"),
            new(TiposDeClaim.TrocarSenha, "1")
        ]);
        log.LogInformation("Administrador inicial criado ({Email}). Ele precisa trocar a senha no primeiro acesso.", email);
    }
}
