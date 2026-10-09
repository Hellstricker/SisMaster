using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.Identidade.Tests;

/// <summary>
/// "Esqueci minha senha" por link: pedir NÃO altera a senha; só quem abre o e-mail define a nova.
/// Resposta igual para qualquer e-mail e um pedido a cada 10 minutos por conta.
/// </summary>
[Collection(ColecaoIdentidade.Nome)]
public class RecuperacaoDeSenhaTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly IdentidadeFactory _fabrica;

    public RecuperacaoDeSenhaTests(IdentidadeFactory fabrica) => _fabrica = fabrica;

    private Task<HttpResponseMessage> Pedir(string email) =>
        _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/esqueci-senha", new { email });

    private Task<HttpResponseMessage> Redefinir(string email, string token, string novaSenha) =>
        _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/redefinir-senha", new { email, token, novaSenha });

    private async Task<HttpResponseMessage> Entrar(string email, string senha) =>
        await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/autenticar", new { email, password = senha });

    private static async Task<string> Mensagem(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("errors").GetProperty("messages")[0].GetString()!;

    [Fact]
    public async Task Pedir_a_recuperacao_nao_altera_a_senha_de_quem_tem_a_conta()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()], senha: "Antiga1234");

        (await Pedir(email)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Entrar(email, "Antiga1234")).StatusCode.Should().Be(HttpStatusCode.OK, "quem só sabe o e-mail não pode tirar a pessoa do sistema");
    }

    [Fact]
    public async Task O_link_do_e_mail_define_a_nova_senha_uma_unica_vez()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()], senha: "Antiga1234");
        await Pedir(email);
        var (e, token) = _fabrica.Emails.LinkEnviadoPara(email);

        var resp = await Redefinir(e, token, "NovaSenha99");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Entrar(email, "NovaSenha99")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Entrar(email, "Antiga1234")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var reuso = await Redefinir(e, token, "OutraSenha77");
        reuso.StatusCode.Should().Be(HttpStatusCode.BadRequest, "o link só vale uma vez");
        (await Entrar(email, "NovaSenha99")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_errado_ou_de_outra_conta_e_recusado_e_nada_muda()
    {
        if (SemBanco) return;
        var dono = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()], senha: "Antiga1234");
        var outro = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()], senha: "Antiga1234");
        await Pedir(dono);
        var (_, tokenDoDono) = _fabrica.Emails.LinkEnviadoPara(dono);

        (await Redefinir(dono, "token-inventado", "NovaSenha99")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Redefinir(outro, tokenDoDono, "NovaSenha99")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await Entrar(dono, "Antiga1234")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Entrar(outro, "Antiga1234")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Senha_fraca_e_recusada_com_o_motivo_e_o_link_continua_valendo()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()]);
        await Pedir(email);
        var (e, token) = _fabrica.Emails.LinkEnviadoPara(email);

        var fraca = await Redefinir(e, token, "abc");

        fraca.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Mensagem(fraca)).Should().NotContain("link", "o problema é a senha, e não o link");
        (await Redefinir(e, token, "SenhaBoa123")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_resposta_e_a_mesma_para_e_mail_existente_inexistente_e_desativado()
    {
        if (SemBanco) return;
        var existente = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()]);
        var desativado = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()], desativado: true);

        var a = await (await Pedir(existente)).Content.ReadAsStringAsync();
        var b = await (await Pedir("ninguem@teste.local")).Content.ReadAsStringAsync();
        var c = await (await Pedir(desativado)).Content.ReadAsStringAsync();

        b.Should().Be(a);
        c.Should().Be(a);
        _fabrica.Emails.Enviados.Should().NotContain(e => e.Para == "ninguem@teste.local" || e.Para == desativado);
    }

    [Fact]
    public async Task Pedidos_seguidos_da_mesma_conta_mandam_um_unico_e_mail()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()]);

        await Pedir(email);
        await Pedir(email);

        _fabrica.Emails.Enviados.Count(e => e.Para == email).Should().Be(1, "o segundo pedido dentro do intervalo é ignorado");
    }

    [Fact]
    public async Task Redefinir_pelo_link_libera_conta_bloqueada_e_tira_a_marca_de_senha_temporaria()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()]);
        for (var i = 0; i < 5; i++) await Entrar(email, "Errada9999");
        await Pedir(email);
        var (e, token) = _fabrica.Emails.LinkEnviadoPara(email);

        (await Redefinir(e, token, "NovaSenha99")).StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await Entrar(email, "NovaSenha99");
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("userToken").GetProperty("trocarSenha").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task E_mail_invalido_e_recusado()
    {
        if (SemBanco) return;
        (await Pedir("isto-nao-e-email")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
