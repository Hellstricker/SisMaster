using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.Identidade.Tests;

/// <summary>"Esqueci minha senha": temporária por e-mail, resposta igual para qualquer e-mail, um pedido a cada 10 minutos.</summary>
[Collection(ColecaoIdentidade.Nome)]
public class RecuperacaoDeSenhaTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly IdentidadeFactory _fabrica;

    public RecuperacaoDeSenhaTests(IdentidadeFactory fabrica) => _fabrica = fabrica;

    private Task<HttpResponseMessage> Pedir(string email) =>
        _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/esqueci-senha", new { email });

    private async Task<HttpResponseMessage> Entrar(string email, string senha) =>
        await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/autenticar", new { email, password = senha });

    [Fact]
    public async Task Envia_temporaria_por_email_invalida_a_antiga_e_exige_troca()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()], senha: "Antiga1234");

        var resp = await Pedir(email);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var nova = _fabrica.Emails.SenhaEnviadaPara(email);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(nova);
        (await Entrar(email, "Antiga1234")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var login = await Entrar(email, nova);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("userToken").GetProperty("trocarSenha").GetBoolean().Should().BeTrue();
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
    public async Task Pedidos_seguidos_da_mesma_conta_nao_geram_outra_senha()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()]);

        await Pedir(email);
        var primeira = _fabrica.Emails.SenhaEnviadaPara(email);
        await Pedir(email);

        _fabrica.Emails.Enviados.Count(e => e.Para == email).Should().Be(1, "o segundo pedido dentro do intervalo é ignorado");
        (await Entrar(email, primeira)).StatusCode.Should().Be(HttpStatusCode.OK, "a senha enviada continua valendo");
    }

    [Fact]
    public async Task Recuperar_libera_conta_bloqueada()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()]);
        for (var i = 0; i < 5; i++) await Entrar(email, "Errada9999");

        await Pedir(email);

        (await Entrar(email, _fabrica.Emails.SenhaEnviadaPara(email))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task E_mail_invalido_e_recusado()
    {
        if (SemBanco) return;
        (await Pedir("isto-nao-e-email")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
