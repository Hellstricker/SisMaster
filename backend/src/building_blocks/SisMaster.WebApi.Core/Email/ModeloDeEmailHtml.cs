using System.Text;
using System.Text.RegularExpressions;

namespace SisMaster.WebApi.Core.Email;

/// <summary>
/// Monta a versão HTML dos e-mails do sistema a partir do texto simples que os serviços já escrevem: parágrafos separados por linha em
/// branco, um endereço (http/https) sozinho numa linha vira botão, endereços no meio do texto viram link. Todo o resto é escapado, então
/// nada do que entra no texto (nome da pessoa, motivo de recusa) vira código. O e-mail sempre vai com o texto simples também
/// (multipart/alternative): quem lê em modo texto, ou um filtro de spam, continua vendo a mesma mensagem.
/// </summary>
public static partial class ModeloDeEmailHtml
{
    private const string Laranja = "#F2661C";
    private const string Quadra = "#0E2A3D";

    [GeneratedRegex(@"https?://[^\s<>""]+", RegexOptions.IgnoreCase)]
    private static partial Regex Endereco();

    public static string Montar(string titulo, string corpoTexto, string nomeDoSistema)
    {
        var html = new StringBuilder();
        var paragrafos = Regex.Split(corpoTexto.Replace("\r\n", "\n").Trim(), @"\n\s*\n");
        foreach (var paragrafo in paragrafos)
        {
            var texto = paragrafo.Trim();
            if (texto.Length == 0) continue;

            var sozinho = Endereco().Match(texto);
            if (sozinho.Success && sozinho.Value.Length == texto.Length)
            {
                html.Append($"<p style=\"margin:20px 0\"><a href=\"{Atributo(texto)}\" style=\"display:inline-block;background:{Laranja};color:#ffffff;text-decoration:none;font-weight:700;padding:12px 22px;border-radius:10px\">Abrir o link</a></p>")
                    .Append($"<p style=\"margin:0 0 16px;font-size:12px;color:#5b6b7a;word-break:break-all\">Se o botão não abrir, copie este endereço no navegador:<br>{Escapar(texto)}</p>");
                continue;
            }
            html.Append("<p style=\"margin:0 0 16px;line-height:1.55\">").Append(ComLinks(texto).Replace("\n", "<br>")).Append("</p>");
        }

        var nome = Escapar(nomeDoSistema);
        return $"""
            <!doctype html>
            <html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>{Escapar(titulo)}</title></head>
            <body style="margin:0;padding:0;background:#FBF7F1;font-family:'Segoe UI',Arial,sans-serif;color:#1b2733">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#FBF7F1"><tr><td align="center" style="padding:24px 12px">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e6ddd0">
            <tr><td style="background:{Quadra};color:#ffffff;padding:18px 24px;font-size:18px;font-weight:800">{nome}</td></tr>
            <tr><td style="padding:24px"><h1 style="margin:0 0 18px;font-size:20px;line-height:1.3;color:{Quadra}">{Escapar(titulo)}</h1>{html}</td></tr>
            <tr><td style="padding:14px 24px;background:#f6f1e9;font-size:12px;color:#5b6b7a">Mensagem automática de {nome}. Não responda a este e-mail.</td></tr>
            </table></td></tr></table></body></html>
            """;
    }

    /// <summary>Escapa o texto e troca cada endereço http/https por um link (sem levar para dentro dele a pontuação final da frase).</summary>
    private static string ComLinks(string texto)
    {
        var saida = new StringBuilder();
        var fim = 0;
        foreach (Match m in Endereco().Matches(texto))
        {
            saida.Append(Escapar(texto[fim..m.Index]));
            var url = m.Value.TrimEnd('.', ',', ';', ':', ')', '!', '?');
            saida.Append($"<a href=\"{Atributo(url)}\" style=\"color:{Laranja}\">{Escapar(url)}</a>");
            saida.Append(Escapar(m.Value[url.Length..]));
            fim = m.Index + m.Length;
        }
        saida.Append(Escapar(texto[fim..]));
        return saida.ToString();
    }

    private static string Atributo(string url) => Escapar(url);

    /// <summary>Escapa só o que é perigoso em HTML (& < > aspas); os acentos seguem como estão, o e-mail é UTF-8.</summary>
    private static string Escapar(string texto) =>
        texto.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
}
