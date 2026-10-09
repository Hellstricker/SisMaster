using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.Identidade.Tests;

/// <summary>Gestão de usuários: quem cria quem, senha temporária só por e-mail, escopo por associação.</summary>
[Collection(ColecaoIdentidade.Nome)]
public class UsuariosTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly IdentidadeFactory _fabrica;

    public UsuariosTests(IdentidadeFactory fabrica) => _fabrica = fabrica;

    private async Task<HttpClient> Cliente(string email, string senha)
    {
        var cliente = _fabrica.CreateClient();
        var resp = await cliente.PostAsJsonAsync("/api/identidade/autenticar", new { email, password = senha });
        resp.EnsureSuccessStatusCode();
        var token = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    private async Task<HttpClient> ComoAdministrador() => await Cliente(await _fabrica.CriarUsuarioAsync("Administrador"), "Senha1234");

    private async Task<(HttpClient Cliente, Guid Associacao)> ComoAdminDeAssociacao()
    {
        var assoc = Guid.NewGuid();
        return (await Cliente(await _fabrica.CriarUsuarioAsync("AdministradorDeAssociacao", associacoes: [assoc]), "Senha1234"), assoc);
    }

    private static async Task<JsonElement> Corpo(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string> IdDe(HttpClient admin, string email) =>
        (await Corpo(await admin.GetAsync("/api/identidade/usuarios"))).EnumerateArray()
            .Single(u => u.GetProperty("email").GetString() == email).GetProperty("id").GetString()!;

    private static string NovoEmail() => $"novo.{Guid.NewGuid():N}@teste.local";

    [Fact]
    public async Task Administrador_cria_usuario_e_a_senha_vai_so_por_email()
    {
        if (SemBanco) return;
        var admin = await ComoAdministrador();
        var email = NovoEmail();

        var resp = await admin.PostAsJsonAsync("/api/identidade/usuarios", new { nome = "Maria Souza", email, perfil = "Arbitro", associacoes = new[] { Guid.NewGuid() } });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var senha = _fabrica.Emails.SenhaEnviadaPara(email);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(senha, "nem o administrador vê a senha temporária");
        senha.Length.Should().BeGreaterThanOrEqualTo(12);

        // A pessoa entra com a senha do e-mail e é obrigada a trocá-la.
        var login = await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/autenticar", new { email, password = senha });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var usuario = (await Corpo(login)).GetProperty("userToken");
        usuario.GetProperty("trocarSenha").GetBoolean().Should().BeTrue();
        usuario.GetProperty("perfil").GetString().Should().Be("Arbitro");
    }

    [Fact]
    public async Task Email_repetido_perfil_invalido_e_perfil_sem_associacao_sao_recusados()
    {
        if (SemBanco) return;
        var admin = await ComoAdministrador();
        var email = NovoEmail();
        var assoc = Guid.NewGuid();
        (await admin.PostAsJsonAsync("/api/identidade/usuarios", new { nome = "Pessoa Um", email, perfil = "Arbitro", associacoes = new[] { assoc } })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.PostAsJsonAsync("/api/identidade/usuarios", new { nome = "Pessoa Dois", email, perfil = "Arbitro", associacoes = new[] { assoc } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/identidade/usuarios", new { nome = "Pessoa Tres", email = NovoEmail(), perfil = "Arbitro", associacoes = Array.Empty<Guid>() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/identidade/usuarios", new { nome = "Pessoa Tres", email = NovoEmail(), perfil = "Inventado", associacoes = new[] { assoc } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Se_o_email_falha_o_usuario_nao_e_criado()
    {
        if (SemBanco) return;
        var admin = await ComoAdministrador();
        var email = NovoEmail();
        _fabrica.Emails.Falhar = true;
        try
        {
            var resp = await admin.PostAsJsonAsync("/api/identidade/usuarios", new { nome = "Sem Email", email, perfil = "Arbitro", associacoes = new[] { Guid.NewGuid() } });
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally { _fabrica.Emails.Falhar = false; }

        var lista = await Corpo(await admin.GetAsync("/api/identidade/usuarios"));
        lista.EnumerateArray().Select(u => u.GetProperty("email").GetString()).Should().NotContain(email);
    }

    [Fact]
    public async Task Admin_de_associacao_so_cria_arbitro_nas_proprias_associacoes()
    {
        if (SemBanco) return;
        var (gestor, assoc) = await ComoAdminDeAssociacao();

        (await gestor.PostAsJsonAsync("/api/identidade/usuarios", new { nome = "Arbitro Meu", email = NovoEmail(), perfil = "Arbitro", associacoes = new[] { assoc } })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await gestor.PostAsJsonAsync("/api/identidade/usuarios", new { nome = "Arbitro Alheio", email = NovoEmail(), perfil = "Arbitro", associacoes = new[] { Guid.NewGuid() } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await gestor.PostAsJsonAsync("/api/identidade/usuarios", new { nome = "Outro Gestor", email = NovoEmail(), perfil = "AdministradorDeAssociacao", associacoes = new[] { assoc } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await gestor.PostAsJsonAsync("/api/identidade/usuarios", new { nome = "Admin Global", email = NovoEmail(), perfil = "Administrador", associacoes = Array.Empty<Guid>() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_de_associacao_so_enxerga_e_mexe_nos_arbitros_dele()
    {
        if (SemBanco) return;
        var (gestor, assoc) = await ComoAdminDeAssociacao();
        var meu = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [assoc]);
        var alheio = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()]);
        var admin = await ComoAdministrador();

        var visiveis = (await Corpo(await gestor.GetAsync("/api/identidade/usuarios"))).EnumerateArray().Select(u => u.GetProperty("email").GetString()).ToList();
        visiveis.Should().Contain(meu).And.NotContain(alheio);

        (await gestor.PostAsync($"/api/identidade/usuarios/{await IdDe(admin, alheio)}/desativar", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await gestor.PostAsync($"/api/identidade/usuarios/{await IdDe(admin, meu)}/desativar", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Arbitro_nao_acessa_a_gestao_de_usuarios()
    {
        if (SemBanco) return;
        var arbitro = await Cliente(await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()]), "Senha1234");
        (await arbitro.GetAsync("/api/identidade/usuarios")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _fabrica.CreateClient().GetAsync("/api/identidade/usuarios")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Desativar_impede_o_login_e_reativar_devolve_o_acesso()
    {
        if (SemBanco) return;
        var admin = await ComoAdministrador();
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()]);
        var id = await IdDe(admin, email);
        async Task<HttpStatusCode> Entrar() => (await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/autenticar", new { email, password = "Senha1234" })).StatusCode;

        (await admin.PostAsync($"/api/identidade/usuarios/{id}/desativar", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Entrar()).Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"/api/identidade/usuarios/{id}/reativar", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Entrar()).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ninguem_desativa_a_propria_conta_nem_muda_o_proprio_perfil()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Administrador");
        var admin = await Cliente(email, "Senha1234");
        var id = await IdDe(admin, email);

        (await admin.PostAsync($"/api/identidade/usuarios/{id}/desativar", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync($"/api/identidade/usuarios/{id}", new { nome = "Eu Mesmo", perfil = "Arbitro", associacoes = new[] { Guid.NewGuid() } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Redefinir_senha_envia_outra_por_email_desbloqueia_e_exige_troca()
    {
        if (SemBanco) return;
        var admin = await ComoAdministrador();
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()], senha: "Antiga1234");
        var id = await IdDe(admin, email);
        for (var i = 0; i < 5; i++)
            await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/autenticar", new { email, password = "Errada9999" });

        var resp = await admin.PostAsync($"/api/identidade/usuarios/{id}/redefinir-senha", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var nova = _fabrica.Emails.SenhaEnviadaPara(email);
        (await resp.Content.ReadAsStringAsync()).Should().NotContain(nova);
        (await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/autenticar", new { email, password = "Antiga1234" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var login = await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/autenticar", new { email, password = nova });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Corpo(login)).GetProperty("userToken").GetProperty("trocarSenha").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Editar_muda_nome_perfil_e_associacoes()
    {
        if (SemBanco) return;
        var admin = await ComoAdministrador();
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", associacoes: [Guid.NewGuid()]);
        var id = await IdDe(admin, email);
        var nova = Guid.NewGuid();

        var resp = await admin.PutAsJsonAsync($"/api/identidade/usuarios/{id}", new { nome = "Nome Novo", perfil = "AdministradorDeAssociacao", associacoes = new[] { nova } });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var u = await Corpo(resp);
        u.GetProperty("nome").GetString().Should().Be("Nome Novo");
        u.GetProperty("perfil").GetString().Should().Be("AdministradorDeAssociacao");
        u.GetProperty("associacoes").EnumerateArray().Select(a => a.GetGuid()).Should().Equal(nova);
    }
}
