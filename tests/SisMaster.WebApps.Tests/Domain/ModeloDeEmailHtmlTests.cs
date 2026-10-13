using System.Net.Mail;
using FluentAssertions;
using SisMaster.WebApi.Core.Email;
using SisMaster.WebApps.WebApi.Email;
using Xunit;

namespace SisMaster.WebApps.Tests.Domain;

/// <summary>I1 — a versão HTML dos e-mails: escapa tudo, faz botão do link sozinho na linha, link no meio do texto e envia junto com o texto simples.</summary>
public class ModeloDeEmailHtmlTests
{
    [Fact]
    public void Paragrafos_viram_p_e_quebra_de_linha_vira_br()
    {
        var html = ModeloDeEmailHtml.Montar("Assunto", "Olá, José!\n\nPrimeira linha\nSegunda linha", "ABABAS");

        html.Should().Contain("<p style=\"margin:0 0 16px;line-height:1.55\">Olá, José!</p>");
        html.Should().Contain("Primeira linha<br>Segunda linha");
        html.Should().Contain("<h1").And.Contain("Assunto");
        html.Should().Contain("ABABAS");
        html.Should().Contain("lang=\"pt-BR\"").And.Contain("charset=\"utf-8\"");
    }

    [Fact]
    public void Link_sozinho_na_linha_vira_botao_com_o_endereco_por_extenso_para_copiar()
    {
        var url = "https://sistema.exemplo.org/confirmar-email?email=a%40b.com&token=abc_123";
        var html = ModeloDeEmailHtml.Montar("Confirme", $"Abra o link:\n\n{url}\n\nVale por 24 horas.", "ABABAS");

        html.Should().Contain("Abrir o link</a>");
        html.Should().Contain("href=\"https://sistema.exemplo.org/confirmar-email?email=a%40b.com&amp;token=abc_123\"", "o & do endereço é escapado no atributo");
        html.Should().Contain("Se o botão não abrir, copie este endereço");
    }

    [Fact]
    public void Link_no_meio_do_texto_vira_link_sem_a_pontuacao_final()
    {
        var html = ModeloDeEmailHtml.Montar("x", "Veja em https://exemplo.org/inscricoes, por favor.", "ABABAS");

        html.Should().Contain("<a href=\"https://exemplo.org/inscricoes\"");
        html.Should().Contain(">https://exemplo.org/inscricoes</a>, por favor.");
    }

    [Fact]
    public void Nada_do_texto_vira_codigo()
    {
        var html = ModeloDeEmailHtml.Montar("<script>alert(1)</script>", "Motivo: <img src=x onerror=alert(2)> & \"aspas\"\n\njavascript:alert(3) http://x.org/\"onmouseover=\"alert(4)", "<b>Nome</b>");

        html.Should().NotContain("<script>").And.NotContain("<img").And.NotContain("<b>Nome</b>");
        html.Should().Contain("&lt;script&gt;").And.Contain("&lt;img src=x onerror=alert(2)&gt;").And.Contain("&amp;");
        html.Should().NotContain("href=\"javascript:", "só endereços http e https viram link");
        html.Should().NotContain("onmouseover=\"alert", "o endereço termina na aspa e não escapa do atributo");
    }

    [Fact]
    public void Mensagem_sai_com_texto_simples_e_html_e_o_nome_do_remetente()
    {
        var o = new OpcoesDeEmail { Remetente = "contato@exemplo.org", NomeExibicao = "ABABAS" };

        using var m = EnviadorDeEmailSmtp.MontarMensagem(o, "pessoa@exemplo.com", "Inscrição aprovada — #2027-0001", "Olá, José!\n\nSeu pedido foi aprovado.");

        m.Subject.Should().Be("Inscrição aprovada — #2027-0001");
        m.From!.DisplayName.Should().Be("ABABAS");
        m.From.Address.Should().Be("contato@exemplo.org");
        m.To.Single().Address.Should().Be("pessoa@exemplo.com");
        m.Body.Should().Contain("Seu pedido foi aprovado.");
        m.AlternateViews.Select(v => v.ContentType.MediaType).Should().Equal("text/plain", "text/html");
        using var html = new StreamReader(m.AlternateViews[1].ContentStream);
        html.ReadToEnd().Should().Contain("Seu pedido foi aprovado.").And.Contain("ABABAS");
    }

    [Fact]
    public async Task O_arquivo_do_email_e_multipart_alternative_com_as_duas_partes_em_utf8()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "sismaster-eml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        try
        {
            using var m = EnviadorDeEmailSmtp.MontarMensagem(new OpcoesDeEmail { Remetente = "contato@exemplo.org", NomeExibicao = "ABABAS" },
                "pessoa@exemplo.com", "Inscrição aprovada", "Olá, José!\n\nhttps://exemplo.org/minhas-inscricoes");
            using (var cliente = new SmtpClient { DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory, PickupDirectoryLocation = pasta })
                await cliente.SendMailAsync(m);

            var eml = await File.ReadAllTextAsync(Directory.GetFiles(pasta).Single());
            eml.Should().Contain("multipart/alternative");
            eml.Should().Contain("text/plain").And.Contain("text/html");
            eml.Should().Contain("charset=utf-8").And.Contain("To: pessoa@exemplo.com");
        }
        finally { Directory.Delete(pasta, true); }
    }

    [Fact]
    public void Sem_nome_configurado_o_remetente_e_SisMaster()
    {
        using var m = EnviadorDeEmailSmtp.MontarMensagem(new OpcoesDeEmail { Usuario = "x@exemplo.org" }, "p@exemplo.com", "a", "b");
        m.From!.DisplayName.Should().Be("SisMaster");
    }
}
