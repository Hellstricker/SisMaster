using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// V1 a V4: a vitrine pública (início, campeonatos, temporada, classificação e jogo) responde sem login, mostra só o que o
/// visitante pode ver e some quando a associação é desativada. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class PublicoVitrineApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public PublicoVitrineApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private async Task<JsonElement> Obter(string url, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var r = await _fabrica.ClienteCom(null).GetAsync(url);
        r.StatusCode.Should().Be(esperado, url);
        var corpo = await r.Content.ReadAsStringAsync();
        return esperado == HttpStatusCode.OK ? JsonDocument.Parse(corpo).RootElement.Clone() : default;
    }

    [Fact]
    public async Task Inicio_mostra_o_jogo_de_hoje_com_as_equipes_o_local_e_nenhum_dado_pessoal()
    {
        if (SemBanco) return;
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        await DadosDeTeste.CriarAsync(admin, "VitrineA");

        var inicio = await Obter("/api/publico/associacoes/VitrineA/inicio");

        inicio.GetProperty("aoVivo").GetArrayLength().Should().Be(0);
        inicio.GetProperty("resultados").GetArrayLength().Should().Be(0);
        var hoje = inicio.GetProperty("jogosDeHoje").EnumerateArray().Single();
        var equipes = new[] { hoje.GetProperty("casa").GetProperty("texto").GetString(), hoje.GetProperty("visitante").GetProperty("texto").GetString() };
        equipes.Should().BeEquivalentTo("Águias", "Ursos");
        hoje.GetProperty("hora").GetString().Should().Be("19:00");
        hoje.GetProperty("local").GetProperty("nome").GetString().Should().Be("Ginásio");
        hoje.GetProperty("status").GetString().Should().Be("Agendado");
        hoje.GetProperty("categoria").GetString().Should().Be("Master 40+");
        var bruto = inicio.GetRawText().ToLowerInvariant();
        bruto.Should().NotContain("cpf").And.NotContain("email").And.NotContain("telefone");
    }

    [Fact]
    public async Task Temporada_traz_equipes_com_a_quantidade_de_atletas_a_tabela_e_a_classificacao()
    {
        if (SemBanco) return;
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var e = await DadosDeTeste.CriarAsync(admin, "VitrineB");

        var campeonatos = await Obter("/api/publico/associacoes/VitrineB/campeonatos");
        campeonatos.GetProperty("temporadas").EnumerateArray().Single().GetProperty("id").GetGuid().Should().Be(e.TemporadaId);

        var t = await Obter($"/api/publico/temporadas/{e.TemporadaId}");
        t.GetProperty("campeonato").GetString().Should().Be("Campeonato");
        t.GetProperty("equipes").EnumerateArray().Select(x => x.GetProperty("nome").GetString()).Should().BeEquivalentTo("Águias", "Ursos");
        t.GetProperty("equipes").EnumerateArray().All(x => x.GetProperty("atletas").GetInt32() == 0).Should().BeTrue();
        t.GetProperty("jogos").GetArrayLength().Should().Be(1);
        t.GetProperty("fases").EnumerateArray().Single().GetProperty("id").GetGuid().Should().Be(e.FaseId);

        var classificacao = await Obter($"/api/publico/fases/{e.FaseId}/classificacao");
        classificacao.GetProperty("nome").GetString().Should().Be("Fase única");
    }

    [Fact]
    public async Task Jogo_sem_sumula_mostra_so_o_cartao_e_ids_desconhecidos_dao_404()
    {
        if (SemBanco) return;
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var e = await DadosDeTeste.CriarAsync(admin, "VitrineC");

        var j = await Obter($"/api/publico/jogos/{e.JogoId}");
        j.GetProperty("jogo").GetProperty("numero").GetInt32().Should().Be(1);
        j.GetProperty("sumula").ValueKind.Should().Be(JsonValueKind.Null);

        await Obter($"/api/publico/jogos/{Guid.NewGuid()}", HttpStatusCode.NotFound);
        await Obter($"/api/publico/temporadas/{Guid.NewGuid()}", HttpStatusCode.NotFound);
        await Obter($"/api/publico/fases/{Guid.NewGuid()}/classificacao", HttpStatusCode.NotFound);
        await Obter("/api/publico/associacoes/NaoExiste/inicio", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Associacao_desativada_some_de_toda_a_vitrine()
    {
        if (SemBanco) return;
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var e = await DadosDeTeste.CriarAsync(admin, "VitrineD");
        await Obter("/api/publico/associacoes/VitrineD/inicio");

        await DadosDeTeste.Enviar(admin, HttpMethod.Patch, $"/api/associacoes/{e.AssociacaoId}/status", new { ativa = false });

        await Obter("/api/publico/associacoes/VitrineD/inicio", HttpStatusCode.NotFound);
        await Obter("/api/publico/associacoes/VitrineD/campeonatos", HttpStatusCode.NotFound);
        await Obter($"/api/publico/temporadas/{e.TemporadaId}", HttpStatusCode.NotFound);
        await Obter($"/api/publico/jogos/{e.JogoId}", HttpStatusCode.NotFound);
        await Obter($"/api/publico/fases/{e.FaseId}/classificacao", HttpStatusCode.NotFound);
    }
}
