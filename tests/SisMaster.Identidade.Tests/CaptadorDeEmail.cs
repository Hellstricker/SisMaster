using SisMaster.Identidade.Api.Email;

namespace SisMaster.Identidade.Tests;

public sealed class CaptadorDeEmail : IEnviadorDeEmail
{
    public List<(string Para, string Assunto, string Corpo)> Enviados { get; } = [];
    public bool Falhar { get; set; }

    public Task EnviarAsync(string para, string assunto, string corpoTexto, CancellationToken ct = default)
    {
        if (Falhar) throw new InvalidOperationException("falha simulada");
        lock (Enviados) Enviados.Add((para, assunto, corpoTexto));
        return Task.CompletedTask;
    }

    public string SenhaEnviadaPara(string email)
    {
        string corpo;
        lock (Enviados) corpo = Enviados.Last(e => e.Para == email).Corpo;
        const string prefixo = "Senha temporária: ";
        return corpo.Split('\n').Single(l => l.StartsWith(prefixo)).Substring(prefixo.Length).Trim();
    }
}
