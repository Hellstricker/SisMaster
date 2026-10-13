using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// L2: os textos legais (política de privacidade e termos de uso) são publicados em versões que nunca se editam: mudança pequena sobe
/// o número secundário (1.1), mudança relevante sobe o principal (2.0), o histórico fica público e só a gestão da associação publica.
/// Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class TextosLegaisApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public TextosLegaisApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private async Task<Guid> NovaAsync(string sigla)
    {
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        await DadosDeTeste.Enviar(admin, HttpMethod.Post, "/api/associacoes", new { nome = $"Associação {sigla}", sigla, uf = "BA" });
        return (await DadosDeTeste.Enviar(admin, HttpMethod.Get, "/api/associacoes")).EnumerateArray()
            .First(a => a.GetProperty("nome").GetString() == $"Associação {sigla}").GetProperty("id").GetGuid();
    }

    private static async Task<(HttpStatusCode Status, JsonElement Json, string Corpo)> Chamar(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        var texto = await resp.Content.ReadAsStringAsync();
        JsonElement json = default;
        if (!string.IsNullOrWhiteSpace(texto)) { try { json = JsonDocument.Parse(texto).RootElement.Clone(); } catch { } }
        return (resp.StatusCode, json, texto);
    }

    private static JsonElement Dados(JsonElement j) => j.ValueKind == JsonValueKind.Object && j.TryGetProperty("data", out var d) ? d : j;

    [Fact]
    public async Task Sem_versao_publicada_vale_a_1_0_com_os_textos_padrao()
    {
        if (SemBanco) return;
        await NovaAsync("LgPadrao");
        var r = await Chamar(_fabrica.ClienteCom(null), HttpMethod.Get, "/api/publico/associacoes/LgPadrao/legal");
        r.Status.Should().Be(HttpStatusCode.OK, r.Corpo);
        r.Json.GetProperty("versao").GetString().Should().Be("1.0");
        r.Json.GetProperty("textoPrivacidade").ValueKind.Should().Be(JsonValueKind.Null);
        r.Json.GetProperty("textoTermos").ValueKind.Should().Be(JsonValueKind.Null);
        r.Json.GetProperty("historico").GetArrayLength().Should().Be(0);
        (await Chamar(_fabrica.ClienteCom(null), HttpMethod.Get, "/api/publico/associacoes/NaoExiste/legal")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Publicar_sobe_a_versao_guarda_o_historico_e_a_vigente_e_a_mais_nova()
    {
        if (SemBanco) return;
        var id = await NovaAsync("LgVersoes");
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));

        var a = await Chamar(admin, HttpMethod.Post, $"/api/associacoes/{id}/textos-legais",
            new { relevante = false, resumo = "Corrigido o e-mail do encarregado", textoPrivacidade = "## 1. Quem somos\nTexto novo de {{nome}}.", textoTermos = (string?)null });
        a.Status.Should().Be(HttpStatusCode.OK, a.Corpo);
        Dados(a.Json).GetProperty("versao").GetString().Should().Be("1.1");

        var b = await Chamar(admin, HttpMethod.Post, $"/api/associacoes/{id}/textos-legais",
            new { relevante = true, resumo = "Novo uso de imagem", textoPrivacidade = "Política 2", textoTermos = "Termos 2" });
        Dados(b.Json).GetProperty("versao").GetString().Should().Be("2.0");

        var c = await Chamar(admin, HttpMethod.Post, $"/api/associacoes/{id}/textos-legais", new { relevante = false, resumo = "Ajuste de redação" });
        Dados(c.Json).GetProperty("versao").GetString().Should().Be("2.1");

        var publico = (await Chamar(_fabrica.ClienteCom(null), HttpMethod.Get, "/api/publico/associacoes/LgVersoes/legal")).Json;
        publico.GetProperty("versao").GetString().Should().Be("2.1");
        publico.GetProperty("textoPrivacidade").ValueKind.Should().Be(JsonValueKind.Null, "a 2.1 não trouxe texto: vale o padrão do sistema");
        publico.GetProperty("resumo").GetString().Should().Be("Ajuste de redação");
        publico.GetProperty("historico").EnumerateArray().Select(h => h.GetProperty("versao").GetString()).Should().Equal("2.1", "2.0", "1.1");
        publico.GetRawText().Should().NotContain("Texto novo de", "o histórico público não traz os textos antigos");

        // a gestão enxerga o histórico com os textos de cada versão e quem publicou
        var gestao = Dados((await Chamar(admin, HttpMethod.Get, $"/api/associacoes/{id}/textos-legais")).Json);
        gestao.GetProperty("vigente").GetString().Should().Be("2.1");
        var v11 = gestao.GetProperty("versoes").EnumerateArray().Single(v => v.GetProperty("versao").GetString() == "1.1");
        v11.GetProperty("textoPrivacidade").GetString().Should().Contain("Texto novo de {{nome}}");
        v11.GetProperty("publicadaPor").GetString().Should().Be("Administrador");
        v11.GetProperty("relevante").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Publicacao_invalida_e_recusada()
    {
        if (SemBanco) return;
        var id = await NovaAsync("LgInval");
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var url = $"/api/associacoes/{id}/textos-legais";

        (await Chamar(admin, HttpMethod.Post, url, new { relevante = false, resumo = "  " })).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Chamar(admin, HttpMethod.Post, url, new { relevante = false, resumo = new string('x', 301) })).Status.Should().Be(HttpStatusCode.BadRequest);
        var longo = await Chamar(admin, HttpMethod.Post, url, new { relevante = false, resumo = "ok", textoTermos = new string('x', 60_001) });
        longo.Status.Should().Be(HttpStatusCode.BadRequest);
        longo.Corpo.Should().Contain("caracteres");

        (await Chamar(admin, HttpMethod.Post, url, new { relevante = false, resumo = "primeira", vigenteDesde = "2030-01-10" })).Status.Should().Be(HttpStatusCode.OK);
        var anterior = await Chamar(admin, HttpMethod.Post, url, new { relevante = false, resumo = "voltando no tempo", vigenteDesde = "2030-01-01" });
        anterior.Status.Should().Be(HttpStatusCode.BadRequest);
        anterior.Corpo.Should().Contain("anterior");

        (await Chamar(admin, HttpMethod.Post, $"/api/associacoes/{Guid.NewGuid()}/textos-legais", new { relevante = false, resumo = "x" })).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task So_a_gestao_da_propria_associacao_publica_e_nao_ha_como_editar_versao_antiga()
    {
        if (SemBanco) return;
        var id = await NovaAsync("LgAcesso");
        var outra = await NovaAsync("LgOutra");
        var url = $"/api/associacoes/{id}/textos-legais";
        var corpo = new { relevante = false, resumo = "x" };

        (await Chamar(_fabrica.ClienteCom(null), HttpMethod.Post, url, corpo)).Status.Should().Be(HttpStatusCode.Unauthorized);
        (await Chamar(_fabrica.ClienteCom(ApiFactory.TokenDoParticipante(Guid.NewGuid(), "p@exemplo.com")), HttpMethod.Post, url, corpo)).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(_fabrica.ClienteCom(ApiFactory.Token("Arbitro", id)), HttpMethod.Get, url)).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(_fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", outra)), HttpMethod.Post, url, corpo)).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(_fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", id)), HttpMethod.Post, url, corpo)).Status.Should().Be(HttpStatusCode.OK);

        // não existe rota para alterar ou apagar uma versão publicada
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        foreach (var metodo in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
            (await Chamar(admin, metodo, url + "/1.1", corpo)).Status.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }
}
