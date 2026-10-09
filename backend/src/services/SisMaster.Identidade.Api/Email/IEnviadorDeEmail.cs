using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace SisMaster.Identidade.Api.Email;

public interface IEnviadorDeEmail
{
    /// <summary>Envia o e-mail. Lança exceção se não conseguir (quem chama decide o que dizer à pessoa).</summary>
    Task EnviarAsync(string para, string assunto, string corpoTexto, CancellationToken ct = default);
}

/// <summary>Desenvolvimento e testes: não envia nada, só registra no log (sem o corpo, que leva a senha).</summary>
public sealed class EnviadorDeEmailLog(ILogger<EnviadorDeEmailLog> log) : IEnviadorDeEmail
{
    public Task EnviarAsync(string para, string assunto, string corpoTexto, CancellationToken ct = default)
    {
        log.LogInformation("[e-mail simulado] para {Para}: {Assunto}", para, assunto);
        return Task.CompletedTask;
    }
}

public sealed class OpcoesDeEmail
{
    public string Provedor { get; set; } = "Log";
    public string? Host { get; set; }
    public int Porta { get; set; } = 587;
    public string? Usuario { get; set; }
    public string? Senha { get; set; }
    public string? Remetente { get; set; }
}

public sealed class EnviadorDeEmailSmtp(IOptions<OpcoesDeEmail> opcoes) : IEnviadorDeEmail
{
    public async Task EnviarAsync(string para, string assunto, string corpoTexto, CancellationToken ct = default)
    {
        var o = opcoes.Value;
        using var cliente = new SmtpClient(o.Host, o.Porta)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(o.Usuario, o.Senha)
        };
        using var mensagem = new MailMessage(o.Remetente ?? o.Usuario!, para, assunto, corpoTexto);
        await cliente.SendMailAsync(mensagem, ct);
    }
}

public static class EmailConfiguration
{
    /// <summary>Email:Provedor = "Smtp" envia de verdade (Email:Host, Porta, Usuario, Senha, Remetente); qualquer outro valor só registra no log.</summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpcoesDeEmail>(configuration.GetSection("Email"));
        if (string.Equals(configuration["Email:Provedor"], "Smtp", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEnviadorDeEmail, EnviadorDeEmailSmtp>();
        else
            services.AddSingleton<IEnviadorDeEmail, EnviadorDeEmailLog>();
        return services;
    }
}
