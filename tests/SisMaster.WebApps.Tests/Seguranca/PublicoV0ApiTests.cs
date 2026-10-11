using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// V0: a API pública do site (/api/publico) responde sem login, só com o que qualquer visitante pode ver, e some com
/// associação inativa ou temporada fora da janela de inscrição. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class PublicoV0ApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public PublicoV0ApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private static async Task<(HttpStatusCode Status, string Corpo)> Chamar(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    /// <summary>Associação com uma categoria, um campeonato e uma temporada cuja janela de inscrição vai de <paramref name="inicio"/> a <paramref name="fim"/>.</summary>
    private async Task<string> NovaAsync(string sigla, int inicioEmDias, int fimEmDias)
    {
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var send = (HttpMethod m, string u, object? b) => DadosDeTeste.Enviar(admin, m, u, b);
        await send(HttpMethod.Post, "/api/associacoes", new { nome = $"Associação {sigla}", sigla, uf = "BA" });
        var assoc = (await send(HttpMethod.Get, "/api/associacoes", null)).EnumerateArray()
            .First(a => a.GetProperty("nome").GetString() == $"Associação {sigla}").GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/associacoes/{assoc}/categorias",
            new { nome = "Master 40+", idadeMinima = 40, sexo = "Masculino", aceitaAbaixoIdadeMinima = false, minimoPeriodosEmQuadra = 1, minimoPeriodosForaQuadra = 1 });
        var categoria = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}/categorias", null)).EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/associacoes/{assoc}/campeonatos", new { nome = "Campeonato Aberto" });
        var campeonato = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}", null)).GetProperty("campeonatos").EnumerateArray().First().GetProperty("id").GetGuid();
        var hoje = DateTime.Now;
        await send(HttpMethod.Post, $"/api/campeonatos/{campeonato}/temporadas",
            new { ano = hoje.Year, dataInicioInscricoes = hoje.AddDays(inicioEmDias), dataFimInscricoes = hoje.AddDays(fimEmDias) });
        var temporada = (await send(HttpMethod.Get, $"/api/campeonatos/{campeonato}", null)).GetProperty("temporadas").EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/categorias", new { categoriaId = categoria });
        return sigla;
    }

    private async Task<JsonElement> InscricoesAbertas(string sigla)
    {
        var r = await Chamar(_fabrica.ClienteCom(null), HttpMethod.Get, $"/api/publico/associacoes/{sigla}/inscricoes-abertas");
        r.Status.Should().Be(HttpStatusCode.OK, r.Corpo);
        return JsonDocument.Parse(r.Corpo).RootElement.GetProperty("temporadas").Clone();
    }

    [Fact]
    public async Task Associacao_e_publica_e_sigla_desconhecida_da_404()
    {
        if (SemBanco) return;
        await NovaAsync("PubA", -1, 30);
        var anonimo = _fabrica.ClienteCom(null);

        var ok = await Chamar(anonimo, HttpMethod.Get, "/api/publico/associacoes/PubA");
        ok.Status.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(ok.Corpo).RootElement;
        json.GetProperty("nome").GetString().Should().Be("Associação PubA");
        json.GetProperty("uf").GetString().Should().Be("BA");

        (await Chamar(anonimo, HttpMethod.Get, "/api/publico/associacoes/NaoExiste")).Status.Should().Be(HttpStatusCode.NotFound);
        (await Chamar(anonimo, HttpMethod.Get, "/api/publico/associacoes/NaoExiste/inscricoes-abertas")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Temporada_com_janela_aberta_aparece_com_categorias_e_sem_dado_pessoal_nem_valor()
    {
        if (SemBanco) return;
        await NovaAsync("PubB", -1, 30);

        var temporadas = await InscricoesAbertas("PubB");

        temporadas.GetArrayLength().Should().Be(1);
        var t = temporadas[0];
        t.GetProperty("campeonato").GetString().Should().Be("Campeonato Aberto");
        t.GetProperty("diasRestantes").GetInt32().Should().BeInRange(29, 30);
        t.GetProperty("categorias").EnumerateArray().Single().GetProperty("nome").GetString().Should().Be("Master 40+");
        var bruto = t.GetRawText().ToLowerInvariant();
        bruto.Should().NotContain("taxa").And.NotContain("cpf").And.NotContain("email");
    }

    [Fact]
    public async Task Janela_ainda_nao_comecou_ou_ja_terminou_nao_aparece()
    {
        if (SemBanco) return;
        await NovaAsync("PubC", 5, 30);   // começa em 5 dias
        await NovaAsync("PubD", -30, -2); // terminou há 2 dias

        (await InscricoesAbertas("PubC")).GetArrayLength().Should().Be(0);
        (await InscricoesAbertas("PubD")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Inscricoes_encerradas_pela_diretoria_somem_da_vitrine()
    {
        if (SemBanco) return;
        await NovaAsync("PubE", -1, 30);
        (await InscricoesAbertas("PubE")).GetArrayLength().Should().Be(1);

        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var assoc = (await DadosDeTeste.Enviar(admin, HttpMethod.Get, "/api/associacoes")).EnumerateArray().First(a => a.GetProperty("nome").GetString() == "Associação PubE").GetProperty("id").GetGuid();
        var camp = (await DadosDeTeste.Enviar(admin, HttpMethod.Get, $"/api/associacoes/{assoc}")).GetProperty("campeonatos").EnumerateArray().First().GetProperty("id").GetGuid();
        var temp = (await DadosDeTeste.Enviar(admin, HttpMethod.Get, $"/api/campeonatos/{camp}")).GetProperty("temporadas").EnumerateArray().First().GetProperty("id").GetGuid();
        await DadosDeTeste.Enviar(admin, HttpMethod.Patch, $"/api/campeonatos/{camp}/temporadas/{temp}/encerrar-inscricoes", new { });

        (await InscricoesAbertas("PubE")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Resposta_publica_leva_cache_curto()
    {
        if (SemBanco) return;
        await NovaAsync("PubF", -1, 30);
        var resp = await _fabrica.ClienteCom(null).GetAsync("/api/publico/associacoes/PubF/inscricoes-abertas");
        resp.Headers.CacheControl!.Public.Should().BeTrue();
        resp.Headers.CacheControl.MaxAge.Should().BeGreaterThan(TimeSpan.Zero);
    }
}
