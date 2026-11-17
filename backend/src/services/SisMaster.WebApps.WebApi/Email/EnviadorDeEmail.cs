using System.Net;
using System.Net.Mail;
using System.Text;
using SisMaster.WebApi.Core.Email;
using Microsoft.Extensions.Options;

namespace SisMaster.WebApps.WebApi.Email;

/// <summary>Envio de e-mail do sistema (inscrição recebida, mudança de situação). Mesma configuração (seção Email) da Identidade.</summary>
public interface IEnviadorDeEmail
{
    /// <summary>Envia o e-mail. Lança exceção se não conseguir (quem chama decide o que fazer: a inscrição já foi gravada).</summary>
    Task EnviarAsync(string para, string assunto, string corpoTexto, CancellationToken ct = default);
}

/// <summary>Desenvolvimento e testes: não envia nada, só registra no log (sem o corpo).</summary>
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

    /// <summary>Nome que aparece como remetente e no topo do e-mail (por exemplo, a sigla da associação). Padrão: SisMaster.</summary>
    public string? NomeExibicao { get; set; }
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
        using var mensagem = MontarMensagem(o, para, assunto, corpoTexto);
        await cliente.SendMailAsync(mensagem, ct);
    }

    /// <summary>E-mail com o texto simples e a versão HTML (multipart/alternative): quem lê em texto, ou um filtro, vê a mesma mensagem.</summary>
    public static MailMessage MontarMensagem(OpcoesDeEmail o, string para, string assunto, string corpoTexto)
    {
        var nome = string.IsNullOrWhiteSpace(o.NomeExibicao) ? "SisMaster" : o.NomeExibicao.Trim();
        var mensagem = new MailMessage
        {
            From = new MailAddress(o.Remetente ?? o.Usuario!, nome, Encoding.UTF8),
            Subject = assunto,
            SubjectEncoding = Encoding.UTF8,
            Body = corpoTexto,
            BodyEncoding = Encoding.UTF8,
        };
        mensagem.To.Add(para);
        mensagem.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(corpoTexto, Encoding.UTF8, "text/plain"));
        mensagem.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(ModeloDeEmailHtml.Montar(assunto, corpoTexto, nome), Encoding.UTF8, "text/html"));
        return mensagem;
    }
}

public static class EmailConfiguration
{
    /// <summary>Email:Provedor = "Smtp" envia de verdade (Email:Host, Porta, Usuario, Senha, Remetente); qualquer outro valor só registra no log.</summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpcoesDeEmail>(configuration.GetSection("Email"));
        var provedor = configuration["Email:Provedor"];
        if (string.Equals(provedor, "Smtp", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEnviadorDeEmail, EnviadorDeEmailSmtp>();
        else if (string.Equals(provedor, "Memoria", StringComparison.OrdinalIgnoreCase))
        {
            var url = configuration["App:UrlPublica"] ?? "http://localhost:3000";
            if (!url.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Email:Provedor = Memoria é só para o ambiente de teste (localhost): ele expõe os e-mails por uma rota pública.");
            services.AddSingleton<EnviadorDeEmailMemoria>();
            services.AddSingleton<IEnviadorDeEmail>(p => p.GetRequiredService<EnviadorDeEmailMemoria>());
        }
        else
            services.AddSingleton<IEnviadorDeEmail, EnviadorDeEmailLog>();
        return services;
    }
}

/// <summary>
/// SÓ PARA O AMBIENTE DE TESTE: guarda os e-mails na memória para o teste de navegador ler o link do convite de equipe (rota /api/teste/emails).
/// Só liga com Email:Provedor = "Memoria" e a subida recusa esse modo fora de localhost: nunca num servidor real.
/// </summary>
public sealed class EnviadorDeEmailMemoria : IEnviadorDeEmail
{
    private readonly List<(DateTimeOffset Em, string Para, string Assunto, string Corpo)> _enviados = [];

    public Task EnviarAsync(string para, string assunto, string corpoTexto, CancellationToken ct = default)
    {
        lock (_enviados)
        {
            _enviados.Add((DateTimeOffset.UtcNow, para, assunto, corpoTexto));
            if (_enviados.Count > 300) _enviados.RemoveAt(0);
        }
        return Task.CompletedTask;
    }

    public IReadOnlyList<object> Mensagens(string para)
    {
        lock (_enviados)
            return _enviados.Where(e => string.Equals(e.Para, para, StringComparison.OrdinalIgnoreCase))
                .Select(e => (object)new { e.Em, e.Assunto, e.Corpo }).ToList();
    }
}
