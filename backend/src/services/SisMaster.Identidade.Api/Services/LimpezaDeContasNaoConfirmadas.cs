using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SisMaster.WebApi.Core.Autorizacao;

namespace SisMaster.Identidade.Api.Services;

/// <summary>
/// Apaga, uma vez por dia, as contas de participante cujo e-mail nunca foi confirmado depois de 7 dias. Essas contas ainda não têm
/// inscrição nem ficha: só o e-mail e o nome digitados no cadastro. Contas confirmadas ou da equipe nunca são tocadas.
/// </summary>
public class LimpezaDeContasNaoConfirmadas(IServiceProvider servicos, ILogger<LimpezaDeContasNaoConfirmadas> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken parada)
    {
        // Espera um pouco depois de subir para não competir com as migrations.
        try { await Task.Delay(TimeSpan.FromMinutes(2), parada); } catch (OperationCanceledException) { return; }
        while (!parada.IsCancellationRequested)
        {
            try
            {
                using var escopo = servicos.CreateScope();
                var apagadas = await LimparAsync(escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(), DateTimeOffset.UtcNow);
                if (apagadas > 0) log.LogInformation("Contas de participante nunca confirmadas apagadas: {Total}", apagadas);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Falha na limpeza de contas não confirmadas");
            }
            try { await Task.Delay(TimeSpan.FromHours(24), parada); } catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>Apaga as contas de participante não confirmadas criadas há mais de 7 dias (em relação a <paramref name="agora"/>); devolve quantas.</summary>
    public static async Task<int> LimparAsync(UserManager<IdentityUser> usuarios, DateTimeOffset agora)
    {
        var limite = agora.AddDays(-RegrasDaContaDoPublico.DiasParaApagarNaoConfirmada).ToUnixTimeSeconds();
        var candidatas = (await usuarios.GetUsersInRoleAsync(Perfis.Participante)).Where(u => !u.EmailConfirmed).ToList();
        var apagadas = 0;
        foreach (var u in candidatas)
        {
            var criada = (await usuarios.GetClaimsAsync(u)).FirstOrDefault(c => c.Type == TiposDeClaim.CriadaEm)?.Value;
            if (long.TryParse(criada, out var quando) && quando < limite && (await usuarios.DeleteAsync(u)).Succeeded) apagadas++;
        }
        return apagadas;
    }
}
