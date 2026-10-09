using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SisMaster.Identidade.Api.Configurations;
using Xunit;

namespace SisMaster.Identidade.Tests;

/// <summary>Login, perfis no token, bloqueio por tentativas e renovação. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).</summary>
[Collection(ColecaoIdentidade.Nome)]
public class LoginTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly IdentidadeFactory _fabrica;

    public LoginTests(IdentidadeFactory fabrica) => _fabrica = fabrica;

    private async Task<(HttpStatusCode Status, JsonElement Corpo)> Entrar(string email, string senha)
    {
        var resp = await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/autenticar", new { email, password = senha });
        var texto = await resp.Content.ReadAsStringAsync();
        return (resp.StatusCode, string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone());
    }

    private static string Mensagem(JsonElement corpo) => corpo.GetProperty("errors").GetProperty("messages")[0].GetString()!;

    [Fact]
    public async Task O_administrador_inicial_e_criado_e_precisa_trocar_a_senha()
    {
        if (SemBanco) return;
        var (status, corpo) = await Entrar(IdentidadeFactory.AdminEmail, IdentidadeFactory.AdminSenha);

        status.Should().Be(HttpStatusCode.OK);
        var token = IdentidadeFactory.Ler(corpo.GetProperty("accessToken").GetString()!);
        token.Claims.Where(c => c.Type == "role").Select(c => c.Value).Should().Equal("Administrador");
        token.Claims.Should().NotContain(c => c.Type == "assoc", "o Administrador acessa todas as associações, sem vínculo");
        token.Claims.Should().Contain(c => c.Type == "trocar-senha" && c.Value == "1");
        (token.ValidTo - token.ValidFrom).TotalMinutes.Should().BeApproximately(30, 1);

        var usuario = corpo.GetProperty("userToken");
        usuario.GetProperty("perfil").GetString().Should().Be("Administrador");
        usuario.GetProperty("nome").GetString().Should().Be("Admin Teste");
        usuario.GetProperty("trocarSenha").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Os_tres_perfis_existem()
    {
        if (SemBanco) return;
        using var escopo = _fabrica.Services.CreateScope();
        var roles = escopo.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var perfil in new[] { "Administrador", "AdministradorDeAssociacao", "Arbitro" })
            (await roles.RoleExistsAsync(perfil)).Should().BeTrue(perfil);
        roles.Roles.Count().Should().Be(3);
    }

    [Fact]
    public async Task O_administrador_inicial_nao_e_recriado_quando_ja_existe_usuario()
    {
        if (SemBanco) return;
        using var escopo = _fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var antes = usuarios.Users.Count();
        var outraConfiguracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:AdminEmail"] = "outro.admin@teste.local",
            ["Bootstrap:AdminSenha"] = "Outra12345"
        }).Build();

        await AdministradorInicial.GarantirAsync(_fabrica.Services, outraConfiguracao);

        usuarios.Users.Count().Should().Be(antes);
        (await usuarios.FindByEmailAsync("outro.admin@teste.local")).Should().BeNull("o administrador inicial só nasce quando não há nenhum usuário");
    }

    [Fact]
    public async Task Senha_errada_e_e_mail_inexistente_dao_a_mesma_resposta()
    {
        if (SemBanco) return;
        var existente = await _fabrica.CriarUsuarioAsync("Arbitro");

        var (s1, errada) = await Entrar(existente, "Errada123");
        var (s2, inexistente) = await Entrar("ninguem@teste.local", "Errada123");

        s1.Should().Be(HttpStatusCode.BadRequest);
        s2.Should().Be(HttpStatusCode.BadRequest);
        Mensagem(errada).Should().Be(Mensagem(inexistente), "a resposta não pode revelar quais e-mails têm conta");
    }

    [Fact]
    public async Task Cinco_erros_bloqueiam_a_conta_e_nem_a_senha_certa_entra()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", senha: "Certa1234");

        for (var i = 1; i <= 4; i++)
            Mensagem((await Entrar(email, $"Errada{i}x")).Corpo).Should().Contain("incorretos");
        Mensagem((await Entrar(email, "Errada5x")).Corpo).Should().Contain("bloqueado", "a 5ª tentativa errada bloqueia");

        var (status, corpo) = await Entrar(email, "Certa1234");
        status.Should().Be(HttpStatusCode.BadRequest);
        Mensagem(corpo).Should().Contain("bloqueado");
    }

    [Fact]
    public async Task Arbitro_leva_as_associacoes_e_uma_sessao_longa()
    {
        if (SemBanco) return;
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", nome: "Marcos Vinícius", associacoes: [a, b]);

        var (status, corpo) = await Entrar(email, "Senha1234");

        status.Should().Be(HttpStatusCode.OK);
        var token = IdentidadeFactory.Ler(corpo.GetProperty("accessToken").GetString()!);
        token.Claims.Where(c => c.Type == "role").Select(c => c.Value).Should().Equal("Arbitro");
        token.Claims.Where(c => c.Type == "assoc").Select(c => c.Value).Should().BeEquivalentTo([a.ToString(), b.ToString()]);
        token.Claims.Single(c => c.Type == "nome").Value.Should().Be("Marcos Vinícius");
        token.Claims.Should().NotContain(c => c.Type == "trocar-senha");
        (token.ValidTo - token.ValidFrom).TotalHours.Should().BeApproximately(8, 0.05, "o árbitro não pode cair no meio de um jogo");
    }

    [Fact]
    public async Task Administrador_de_associacao_tem_sessao_curta_e_so_as_associacoes_vinculadas()
    {
        if (SemBanco) return;
        var a = Guid.NewGuid();
        var email = await _fabrica.CriarUsuarioAsync("AdministradorDeAssociacao", associacoes: [a]);

        var (_, corpo) = await Entrar(email, "Senha1234");

        var token = IdentidadeFactory.Ler(corpo.GetProperty("accessToken").GetString()!);
        token.Claims.Where(c => c.Type == "assoc").Select(c => c.Value).Should().Equal(a.ToString());
        (token.ValidTo - token.ValidFrom).TotalMinutes.Should().BeApproximately(30, 1);
    }

    [Fact]
    public async Task Conta_desativada_nao_entra_nem_com_a_senha_certa()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", senha: "Certa1234", desativado: true);

        var (status, corpo) = await Entrar(email, "Certa1234");

        status.Should().Be(HttpStatusCode.BadRequest);
        Mensagem(corpo).Should().Contain("desativada");
    }

    [Fact]
    public async Task Renovar_exige_login_e_entrega_um_novo_token()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("AdministradorDeAssociacao", associacoes: [Guid.NewGuid()]);
        var cliente = _fabrica.CreateClient();

        (await cliente.PostAsync("/api/identidade/renovar", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (_, corpo) = await Entrar(email, "Senha1234");
        var antigo = corpo.GetProperty("accessToken").GetString()!;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", antigo);
        var resposta = await cliente.PostAsync("/api/identidade/renovar", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var novo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString();
        novo.Should().NotBeNullOrEmpty().And.NotBe(antigo);
    }

    [Fact]
    public async Task Renovar_recusa_quem_foi_desativado_depois_de_entrar()
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro");
        var (_, corpo) = await Entrar(email, "Senha1234");

        using (var escopo = _fabrica.Services.CreateScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            await usuarios.AddClaimAsync((await usuarios.FindByEmailAsync(email))!, new("desativado", "1"));
        }

        var cliente = _fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", corpo.GetProperty("accessToken").GetString());
        (await cliente.PostAsync("/api/identidade/renovar", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> ClienteLogado(string email, string senha)
    {
        var (_, corpo) = await Entrar(email, senha);
        var cliente = _fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", corpo.GetProperty("accessToken").GetString());
        return cliente;
    }

    [Fact]
    public async Task Trocar_a_senha_tira_a_marca_de_senha_temporaria_e_a_antiga_deixa_de_valer()
    {
        if (SemBanco) return;
        // conta com senha temporária, como a do administrador inicial
        var email = await _fabrica.CriarUsuarioAsync("Administrador", senha: "Temporaria1");
        using (var escopo = _fabrica.Services.CreateScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            await usuarios.AddClaimAsync((await usuarios.FindByEmailAsync(email))!, new("trocar-senha", "1"));
        }
        var cliente = await ClienteLogado(email, "Temporaria1");

        var resposta = await cliente.PostAsJsonAsync("/api/identidade/trocar-senha", new { senhaAtual = "Temporaria1", novaSenha = "NovaSenha2" });

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
        IdentidadeFactory.Ler(corpo.GetProperty("accessToken").GetString()!).Claims.Should().NotContain(c => c.Type == "trocar-senha");
        corpo.GetProperty("userToken").GetProperty("trocarSenha").GetBoolean().Should().BeFalse();
        (await Entrar(email, "Temporaria1")).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Entrar(email, "NovaSenha2")).Status.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Errada1234", "NovaSenha2", "Senha atual incorreta")]
    [InlineData("Senha1234", "Senha1234", "diferente da atual")]
    [InlineData("Senha1234", "curta1", "pelo menos 8")]
    [InlineData("Senha1234", "SomenteLetras", "dígito")]
    public async Task Trocar_a_senha_recusa_os_casos_invalidos(string atual, string nova, string trecho)
    {
        if (SemBanco) return;
        var email = await _fabrica.CriarUsuarioAsync("Arbitro", senha: "Senha1234");
        var cliente = await ClienteLogado(email, "Senha1234");

        var resposta = await cliente.PostAsJsonAsync("/api/identidade/trocar-senha", new { senhaAtual = atual, novaSenha = nova });

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain(trecho);
        (await Entrar(email, "Senha1234")).Status.Should().Be(HttpStatusCode.OK, "a senha não pode ter mudado");
    }

    [Fact]
    public async Task Trocar_a_senha_exige_login()
    {
        if (SemBanco) return;
        var resposta = await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/trocar-senha", new { senhaAtual = "x", novaSenha = "y" });
        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Nao_existe_cadastro_aberto()
    {
        if (SemBanco) return;
        var resposta = await _fabrica.CreateClient().PostAsJsonAsync("/api/identidade/nova-conta",
            new { email = "intruso@teste.local", password = "Senha1234", confirmPassword = "Senha1234", perfil = "Administrador" });

        resposta.IsSuccessStatusCode.Should().BeFalse();
        using var escopo = _fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        (await usuarios.FindByEmailAsync("intruso@teste.local")).Should().BeNull();
    }

    [Theory]
    [InlineData("12345678", false)]      // sem letra
    [InlineData("Abcdefgh", false)]      // sem número
    [InlineData("Abc1234", false)]       // curta
    [InlineData("Abcdefg1", true)]
    [InlineData("senha2026", true)]
    public async Task A_senha_precisa_de_8_caracteres_com_letra_e_numero(string senha, bool valida)
    {
        if (SemBanco) return;
        using var escopo = _fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var usuario = new IdentityUser { UserName = $"{Guid.NewGuid():N}@teste.local", Email = $"{Guid.NewGuid():N}@teste.local" };

        var resultado = await usuarios.CreateAsync(usuario, senha);

        resultado.Succeeded.Should().Be(valida, string.Join("; ", resultado.Errors.Select(e => e.Description)));
    }
}
