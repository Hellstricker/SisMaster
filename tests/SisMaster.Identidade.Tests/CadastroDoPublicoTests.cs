using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SisMaster.Identidade.Api.Controllers;
using SisMaster.Identidade.Api.Services;
using Xunit;

namespace SisMaster.Identidade.Tests;

/// <summary>
/// I0 — conta do público (perfil Participante): cadastro aberto, e-mail confirmado obrigatório, resposta igual para e-mail novo ou
/// repetido, campo-isca, aceite dos termos registrado, reenvio com intervalo, limpeza de contas nunca confirmadas e o Participante
/// fora da gestão de usuários. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoIdentidade.Nome)]
public class CadastroDoPublicoTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly IdentidadeFactory _fabrica;

    public CadastroDoPublicoTests(IdentidadeFactory fabrica) => _fabrica = fabrica;

    private static string NovoEmail() => $"participante.{Guid.NewGuid():N}@teste.local";

    private Task<HttpResponseMessage> Cadastrar(string email, string senha = "Senha1234", bool aceite = true, string? voltar = null, string? isca = null, HttpClient? cliente = null) =>
        (cliente ?? _fabrica.CreateClient()).PostAsJsonAsync("/api/identidade/cadastrar",
            new { nome = "José Santana", email, senha, aceiteTermos = aceite, versaoTermos = "1.0", voltar, site = isca });

    private Task<HttpResponseMessage> Entrar(string email, string senha = "Senha1234") =>
        _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/autenticar", new { email, password = senha });

    private Task<HttpResponseMessage> Confirmar(string email, string token) =>
        _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/confirmar-email", new { email, token });

    private static async Task<string> Mensagem(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("errors").GetProperty("messages")[0].GetString()!;

    private async Task<string> ContaConfirmadaAsync()
    {
        var email = NovoEmail();
        (await Cadastrar(email)).StatusCode.Should().Be(HttpStatusCode.OK);
        var (e, token, _) = _fabrica.Emails.ConfirmacaoEnviadaPara(email);
        (await Confirmar(e, token)).StatusCode.Should().Be(HttpStatusCode.OK);
        return email;
    }

    [Fact]
    public async Task Cadastro_cria_conta_nao_confirmada_registra_o_aceite_e_envia_o_link()
    {
        if (SemBanco) return;
        var email = NovoEmail();

        var r = await Cadastrar(email, voltar: "/inscricao/abc-123");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await r.Content.ReadAsStringAsync()).Should().Contain("enviamos um link");
        _fabrica.Emails.Quantos(email, "Confirme").Should().Be(1);
        var (_, _, voltar) = _fabrica.Emails.ConfirmacaoEnviadaPara(email);
        voltar.Should().Be("/inscricao/abc-123");

        using var escopo = _fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var u = (await usuarios.FindByEmailAsync(email))!;
        u.EmailConfirmed.Should().BeFalse();
        (await usuarios.GetRolesAsync(u)).Should().Equal("Participante");
        var claims = await usuarios.GetClaimsAsync(u);
        claims.Single(c => c.Type == "termos-versao").Value.Should().Be("1.0");
        claims.Should().Contain(c => c.Type == "termos-em");
        claims.Single(c => c.Type == "nome").Value.Should().Be("José Santana");
    }

    [Fact]
    public async Task Sem_confirmar_o_e_mail_o_login_e_recusado_com_a_mensagem_propria()
    {
        if (SemBanco) return;
        var email = NovoEmail();
        await Cadastrar(email);

        var r = await Entrar(email);

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Mensagem(r)).Should().Be(AuthController.MensagemEmailNaoConfirmado);
        (await Mensagem(await Entrar(email, "SenhaErrada1"))).Should().Be("E-mail ou senha incorretos.", "com a senha errada não se descobre que a conta existe");
    }

    [Fact]
    public async Task O_link_confirma_loga_a_pessoa_como_participante_e_so_vale_uma_vez()
    {
        if (SemBanco) return;
        var email = NovoEmail();
        await Cadastrar(email);
        var (e, token, _) = _fabrica.Emails.ConfirmacaoEnviadaPara(email);

        var r = await Confirmar(e, token);

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
        var jwt = IdentidadeFactory.Ler(corpo.GetProperty("accessToken").GetString()!);
        jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).Should().Equal("Participante");
        jwt.Claims.Should().NotContain(c => c.Type == "assoc");
        corpo.GetProperty("userToken").GetProperty("perfil").GetString().Should().Be("Participante");

        (await Confirmar(e, token)).StatusCode.Should().Be(HttpStatusCode.BadRequest, "link de uso único");
        (await Entrar(email)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Link_adulterado_ou_de_outra_conta_da_a_mesma_mensagem()
    {
        if (SemBanco) return;
        var email = NovoEmail();
        await Cadastrar(email);
        var (e, token, _) = _fabrica.Emails.ConfirmacaoEnviadaPara(email);

        var adulterado = await Confirmar(e, token + "x");
        var inexistente = await Confirmar("ninguem@teste.local", token);

        adulterado.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Mensagem(adulterado)).Should().Be(CadastroController.LinkInvalido);
        (await Mensagem(inexistente)).Should().Be(CadastroController.LinkInvalido);
    }

    [Fact]
    public async Task Email_ja_cadastrado_recebe_a_mesma_resposta_sem_conta_nova_e_sem_link_de_confirmacao()
    {
        if (SemBanco) return;
        var email = await ContaConfirmadaAsync();
        var confirmacoesAntes = _fabrica.Emails.Quantos(email, "Confirme");

        var novo = await Cadastrar(NovoEmail());
        var repetido = await Cadastrar(email, senha: "OutraSenha99");

        repetido.StatusCode.Should().Be(HttpStatusCode.OK);
        (await repetido.Content.ReadAsStringAsync()).Should().Be(await novo.Content.ReadAsStringAsync(), "ninguém descobre quem já tem conta");
        _fabrica.Emails.Quantos(email, "Confirme").Should().Be(confirmacoesAntes, "não manda link de confirmação para quem já tem conta");
        _fabrica.Emails.Quantos(email, "Você já tem uma conta").Should().Be(1);
        (await Entrar(email)).StatusCode.Should().Be(HttpStatusCode.OK, "a senha original continua valendo");
        (await Entrar(email, "OutraSenha99")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sem_aceite_ou_com_senha_fraca_o_cadastro_e_recusado()
    {
        if (SemBanco) return;
        var semAceite = await Cadastrar(NovoEmail(), aceite: false);
        semAceite.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Mensagem(semAceite)).Should().Contain("Termos de uso");

        var curta = await Cadastrar(NovoEmail(), senha: "abc1");
        curta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var semNumero = await Cadastrar(NovoEmail(), senha: "somenteletras");
        semNumero.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var semLetra = await Cadastrar(NovoEmail(), senha: "123456789");
        semLetra.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Cadastrar("nao-e-email", "Senha1234")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Campo_isca_preenchido_nao_cria_conta_nem_manda_e_mail_mas_responde_igual()
    {
        if (SemBanco) return;
        var email = NovoEmail();

        var r = await Cadastrar(email, isca: "http://spam.exemplo");

        r.StatusCode.Should().Be(HttpStatusCode.OK);
        _fabrica.Emails.Quantos(email, "").Should().Be(0);
        using var escopo = _fabrica.Services.CreateScope();
        (await escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email)).Should().BeNull();
    }

    [Theory]
    [InlineData("https://outro-site.com/x", null)]
    [InlineData("//outro-site.com", null)]
    [InlineData("/inscricao/abc?x=1", null)]
    [InlineData("/inscricao/abc", "/inscricao/abc")]
    public async Task O_caminho_de_retorno_so_vale_se_for_interno_do_site(string voltar, string? esperado)
    {
        if (SemBanco) return;
        var email = NovoEmail();
        await Cadastrar(email, voltar: voltar);

        _fabrica.Emails.ConfirmacaoEnviadaPara(email).Voltar.Should().Be(esperado);
    }

    [Fact]
    public async Task Reenvio_respeita_o_intervalo_de_10_minutos_e_o_link_novo_funciona()
    {
        if (SemBanco) return;
        var email = NovoEmail();
        await Cadastrar(email);
        var cliente = _fabrica.CreateClient();

        var logo = await cliente.PostAsJsonAsync("/api/identidade/reenviar-confirmacao", new { email });
        logo.StatusCode.Should().Be(HttpStatusCode.OK);
        _fabrica.Emails.Quantos(email, "Confirme").Should().Be(1, "o primeiro e-mail acabou de sair: não manda outro antes de 10 minutos");

        // Passados os 10 minutos (volta a marca do último envio no tempo), o reenvio sai e o link novo confirma.
        using (var escopo = _fabrica.Services.CreateScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var u = (await usuarios.FindByEmailAsync(email))!;
            var marca = (await usuarios.GetClaimsAsync(u)).Single(c => c.Type == "confirmacao-em");
            await usuarios.ReplaceClaimAsync(u, marca, new System.Security.Claims.Claim("confirmacao-em", DateTimeOffset.UtcNow.AddMinutes(-11).ToUnixTimeSeconds().ToString()));
        }
        await cliente.PostAsJsonAsync("/api/identidade/reenviar-confirmacao", new { email });
        _fabrica.Emails.Quantos(email, "Confirme").Should().Be(2);

        var (e, token, _) = _fabrica.Emails.ConfirmacaoEnviadaPara(email);
        (await Confirmar(e, token)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Conta já confirmada ou inexistente: mesma resposta, nada enviado.
        var resp = await cliente.PostAsJsonAsync("/api/identidade/reenviar-confirmacao", new { email = "ninguem@teste.local" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        _fabrica.Emails.Quantos(email, "Confirme").Should().Be(2);
    }

    [Fact]
    public async Task Esqueci_a_senha_do_participante_manda_para_o_site_e_a_redefinicao_confirma_o_e_mail()
    {
        if (SemBanco) return;
        var email = NovoEmail();
        await Cadastrar(email);   // ainda não confirmou

        await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/esqueci-senha", new { email });
        var (e, token) = _fabrica.Emails.LinkEnviadoPara(email);
        string corpo;
        lock (_fabrica.Emails.Enviados) corpo = _fabrica.Emails.Enviados.Last(x => x.Para == email && x.Assunto.StartsWith("Recuperação")).Corpo;
        corpo.Should().Contain("/redefinir-senha?").And.NotContain("/admin/redefinir-senha", "o participante usa as telas do site");

        var r = await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/redefinir-senha", new { email = e, token, novaSenha = "NovaSenha99" });
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Entrar(email, "NovaSenha99")).StatusCode.Should().Be(HttpStatusCode.OK, "quem abriu o link do e-mail provou que controla o e-mail");
    }

    [Fact]
    public async Task Limpeza_apaga_so_participante_nunca_confirmado_com_mais_de_7_dias()
    {
        if (SemBanco) return;
        var naoConfirmada = NovoEmail();
        await Cadastrar(naoConfirmada);
        var confirmada = await ContaConfirmadaAsync();
        var arbitro = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()]);

        using var escopo = _fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        (await LimpezaDeContasNaoConfirmadas.LimparAsync(usuarios, DateTimeOffset.UtcNow.AddDays(6))).Should().Be(0, "ainda dentro dos 7 dias");
        var apagadas = await LimpezaDeContasNaoConfirmadas.LimparAsync(usuarios, DateTimeOffset.UtcNow.AddDays(8));

        apagadas.Should().BeGreaterThanOrEqualTo(1);
        (await usuarios.FindByEmailAsync(naoConfirmada)).Should().BeNull();
        (await usuarios.FindByEmailAsync(confirmada)).Should().NotBeNull("conta confirmada nunca é apagada");
        (await usuarios.FindByEmailAsync(arbitro)).Should().NotBeNull("conta da equipe nunca é apagada");
    }

    [Fact]
    public async Task Participante_nao_aparece_nem_e_criado_pela_gestao_de_usuarios()
    {
        if (SemBanco) return;
        var conta = await ContaConfirmadaAsync();
        var emailAdmin = IdentidadeFactory.AdminEmail;
        var cliente = _fabrica.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/identidade/autenticar", new { email = emailAdmin, password = IdentidadeFactory.AdminSenha });
        var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString()!;
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var lista = JsonDocument.Parse(await (await cliente.GetAsync("/api/identidade/usuarios")).Content.ReadAsStringAsync()).RootElement;
        var itens = lista.ValueKind == JsonValueKind.Array ? lista : lista.GetProperty("data");
        itens.EnumerateArray().Select(u => u.GetProperty("email").GetString()).Should().NotContain(conta);

        var criar = await cliente.PostAsJsonAsync("/api/identidade/usuarios", new { nome = "Fulano Teste", email = NovoEmail(), perfil = "Participante", associacoes = new[] { Guid.NewGuid() } });
        criar.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_rota_de_e_mails_de_teste_nao_existe_fora_do_modo_memoria()
    {
        if (SemBanco) return;
        var r = await _fabrica.CreateClient().GetAsync("/api/identidade/teste/emails?para=qualquer@teste.local");
        r.StatusCode.Should().Be(HttpStatusCode.NotFound, "o ambiente real nunca expõe e-mails por rota pública");
    }

    [Fact]
    public async Task Cadastros_demais_do_mesmo_endereco_sao_limitados()
    {
        if (SemBanco) return;
        await using var limitada = _fabrica.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Seguranca:LimiteDeCadastrosPorHora"] = "3" })));
        var cliente = limitada.CreateClient();

        var respostas = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++) respostas.Add((await Cadastrar(NovoEmail(), cliente: cliente)).StatusCode);

        respostas.Count(s => s == HttpStatusCode.OK).Should().Be(3);
        respostas.Count(s => s == HttpStatusCode.TooManyRequests).Should().Be(2);
    }
}
