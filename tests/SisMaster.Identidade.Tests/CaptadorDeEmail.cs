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

    /// <summary>Lê o link de redefinição do último e-mail enviado a <paramref name="email"/>: devolve e-mail e token já decodificados.</summary>
    public (string Email, string Token) LinkEnviadoPara(string email)
    {
        string corpo;
        lock (Enviados) corpo = Enviados.Last(e => e.Para == email).Corpo;
        var linha = corpo.Split('\n').Single(l => l.Contains("/redefinir-senha?")).Trim();
        var consulta = System.Web.HttpUtility.ParseQueryString(new Uri(linha).Query);
        return (consulta["email"]!, consulta["token"]!);
    }

    /// <summary>Lê o link de confirmação de e-mail do último e-mail enviado a <paramref name="email"/>: e-mail, token e o caminho de retorno (se houver).</summary>
    public (string Email, string Token, string? Voltar) ConfirmacaoEnviadaPara(string email)
    {
        string corpo;
        lock (Enviados) corpo = Enviados.Last(e => e.Para == email && e.Assunto.StartsWith("Confirme")).Corpo;
        var linha = corpo.Split('\n').Single(l => l.Contains("/confirmar-email?")).Trim();
        var consulta = System.Web.HttpUtility.ParseQueryString(new Uri(linha).Query);
        return (consulta["email"]!, consulta["token"]!, consulta["voltar"]);
    }

    public int Quantos(string email, string assuntoComeca) { lock (Enviados) return Enviados.Count(e => e.Para == email && e.Assunto.StartsWith(assuntoComeca)); }

    public string SenhaEnviadaPara(string email)
    {
        string corpo;
        lock (Enviados) corpo = Enviados.Last(e => e.Para == email).Corpo;
        const string prefixo = "Senha temporária: ";
        return corpo.Split('\n').Single(l => l.StartsWith(prefixo)).Substring(prefixo.Length).Trim();
    }
}
