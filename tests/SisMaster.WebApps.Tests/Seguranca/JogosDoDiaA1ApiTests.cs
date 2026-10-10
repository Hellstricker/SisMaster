using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// A1 pela API de verdade: o que a tela "Jogos do dia" do árbitro lista (só agendados e em andamento, com data), a ordem,
/// a situação da súmula e o "aguardando equipes". Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class JogosDoDiaA1ApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public JogosDoDiaA1ApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private static async Task<(HttpStatusCode Status, string Corpo)> Chamar(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private sealed record Cenario(HttpClient Admin, HttpClient Arbitro, Guid AssociacaoId, Guid TemporadaId, Guid Local, List<Guid> Corridos, Guid Final);

    /// <summary>Águias, Ursos e Lobos (3 jogos de pontos corridos) e uma final entre o 1º e o 2º, com as equipes ainda indefinidas.</summary>
    private async Task<Cenario> NovoAsync(string nome)
    {
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var send = (HttpMethod m, string u, object? b) => DadosDeTeste.Enviar(admin, m, u, b);

        await send(HttpMethod.Post, "/api/associacoes", new { nome = $"Associação {nome}", sigla = nome.Length > 8 ? nome[..8] : nome, uf = "BA" });
        var assoc = (await send(HttpMethod.Get, "/api/associacoes", null)).EnumerateArray()
            .First(a => a.GetProperty("nome").GetString() == $"Associação {nome}").GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/associacoes/{assoc}/categorias",
            new { nome = "Master 40+", idadeMinima = 40, sexo = "Masculino", aceitaAbaixoIdadeMinima = false, minimoPeriodosEmQuadra = 1, minimoPeriodosForaQuadra = 1 });
        var categoria = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}/categorias", null)).EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/associacoes/{assoc}/locais", new { nome = "Ginásio", cidade = "Salvador", estado = "BA" });
        var local = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}/locais", null)).EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/associacoes/{assoc}/campeonatos", new { nome = "Campeonato" });
        var campeonato = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}", null)).GetProperty("campeonatos").EnumerateArray().First().GetProperty("id").GetGuid();
        var hoje = DateTime.UtcNow;
        await send(HttpMethod.Post, $"/api/campeonatos/{campeonato}/temporadas", new { ano = hoje.Year, dataInicioInscricoes = hoje.AddDays(-1), dataFimInscricoes = hoje.AddDays(30) });
        var temporada = (await send(HttpMethod.Get, $"/api/campeonatos/{campeonato}", null)).GetProperty("temporadas").EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/categorias", new { categoriaId = categoria });
        var tc = (await send(HttpMethod.Get, $"/api/temporadas/{temporada}", null)).GetProperty("categorias").EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Patch, $"/api/campeonatos/{campeonato}/temporadas/{temporada}/encerrar-inscricoes", new { });
        foreach (var e in new[] { "Águias", "Ursos", "Lobos" })
            await send(HttpMethod.Post, $"/api/temporadas/{temporada}/equipes", new { temporadaId = temporada, temporadaCategoriaId = tc, nome = e, cor = "#ff0000" });

        async Task<Guid> Fase(object corpo)
        {
            async Task<HashSet<Guid>> Ids()
            {
                var j = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{temporada}/fases")).Corpo).RootElement;
                return (j.TryGetProperty("data", out var x) ? x : j).GetProperty("categorias")[0].GetProperty("fases").EnumerateArray().Select(f => f.GetProperty("id").GetGuid()).ToHashSet();
            }
            var antes = await Ids();
            await send(HttpMethod.Post, $"/api/temporadas/{temporada}/fases", corpo);
            return (await Ids()).First(id => !antes.Contains(id));
        }
        var corridos = await Fase(new { temporadaId = temporada, temporadaCategoriaId = tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        var final = await Fase(new { temporadaId = temporada, temporadaCategoriaId = tc, nome = "Final", tipo = "MataMata", jogosPorConfronto = 1, numeroConfrontos = 1, faseAnteriorId = corridos });
        await send(HttpMethod.Put, $"/api/fases/{final}/confrontos/1", new
        {
            nome = "Final",
            origemA = new { tipo = "Colocacao", faseId = corridos, posicao = 1 },
            origemB = new { tipo = "Colocacao", faseId = corridos, posicao = 2 },
        });
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/fases/encerrar-cadastro", new { });
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/tabela-jogos", new { });

        var jogos = (await send(HttpMethod.Get, $"/api/temporadas/{temporada}/jogos", null)).GetProperty("jogos").EnumerateArray()
            .OrderBy(j => j.GetProperty("numero").GetInt32()).ToList();
        var ids = jogos.Where(j => j.GetProperty("fase").GetProperty("nome").GetString() == "Pontos").Select(j => j.GetProperty("id").GetGuid()).ToList();
        var idFinal = jogos.First(j => j.GetProperty("fase").GetProperty("nome").GetString() == "Final").GetProperty("id").GetGuid();
        return new Cenario(admin, _fabrica.ClienteCom(ApiFactory.Token("Arbitro", assoc)), assoc, temporada, local, ids, idFinal);
    }

    private static Task<(HttpStatusCode Status, string Corpo)> Agendar(Cenario c, Guid jogo, string data, string? hora) =>
        Chamar(c.Admin, HttpMethod.Patch, $"/api/jogos/{jogo}", new { data, hora = hora is null ? null : $"{hora}:00", localId = c.Local });

    /// <summary>Os jogos desta temporada que a tela do árbitro enxerga, na ordem da lista.</summary>
    private static async Task<List<JsonElement>> DoArbitro(Cenario c)
    {
        var json = JsonDocument.Parse((await Chamar(c.Arbitro, HttpMethod.Get, "/api/arbitros/jogos")).Corpo).RootElement;
        return json.GetProperty("jogos").EnumerateArray()
            .Where(j => j.GetProperty("temporada").GetProperty("id").GetGuid() == c.TemporadaId).Select(j => j.Clone()).ToList();
    }

    [Fact]
    public async Task Lista_so_jogos_com_data_e_na_ordem_de_data_e_hora()
    {
        if (SemBanco) return;
        var c = await NovoAsync("A1Ordem");
        (await DoArbitro(c)).Should().BeEmpty("nenhum jogo foi agendado ainda");

        await Agendar(c, c.Corridos[0], "2026-11-15", "11:00");
        await Agendar(c, c.Corridos[1], "2026-11-08", "16:00");
        // o 3º jogo continua sem data: o árbitro não o vê

        var lista = await DoArbitro(c);

        lista.Select(j => j.GetProperty("id").GetGuid()).Should().Equal(c.Corridos[1], c.Corridos[0]);
        lista.Select(j => j.GetProperty("data").GetString()).Should().Equal("2026-11-08", "2026-11-15");
        lista[0].GetProperty("hora").GetString().Should().Be("16:00:00");
        lista[0].GetProperty("local").GetProperty("nome").GetString().Should().Be("Ginásio");
        lista[0].GetProperty("sumula").ValueKind.Should().Be(JsonValueKind.Null);
        lista[0].GetProperty("podePrepararSumula").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Jogo_com_equipes_indefinidas_aparece_como_aguardando_equipes()
    {
        if (SemBanco) return;
        var c = await NovoAsync("A1Aguardando");
        await Agendar(c, c.Final, "2026-11-22", "10:00");

        var j = (await DoArbitro(c)).Single();

        j.GetProperty("podePrepararSumula").GetBoolean().Should().BeFalse();
        j.GetProperty("casa").GetProperty("definida").GetBoolean().Should().BeFalse();
        j.GetProperty("casa").GetProperty("texto").GetString().Should().NotBeNullOrWhiteSpace("mostra a origem, como '1º da fase'");
        j.GetProperty("etiqueta").GetString().Should().Contain("Final");
    }

    [Fact]
    public async Task A_situacao_da_sumula_acompanha_e_o_jogo_com_wo_sai_da_lista_e_volta_ao_desfazer()
    {
        if (SemBanco) return;
        var c = await NovoAsync("A1Situacao");
        foreach (var (jogo, hora) in c.Corridos.Zip(new[] { "08:30", "10:00", "11:30" }))
            await Agendar(c, jogo, "2026-11-08", hora);
        (await DoArbitro(c)).Should().HaveCount(3);

        // Preparar a súmula: continua na lista, agora com a súmula em preparação.
        await DadosDeTeste.Enviar(c.Arbitro, HttpMethod.Post, $"/api/jogos/{c.Corridos[0]}/sumula");
        var primeiro = (await DoArbitro(c)).First(j => j.GetProperty("id").GetGuid() == c.Corridos[0]);
        primeiro.GetProperty("sumula").GetProperty("status").GetString().Should().Be("EmPreparacao");

        // W.O. tira o jogo da lista (já foi decidido).
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/jogos/{c.Corridos[1]}/wo", new { casaAusente = true })).Status.Should().Be(HttpStatusCode.NoContent);
        var depoisDoWO = await DoArbitro(c);
        depoisDoWO.Select(j => j.GetProperty("id").GetGuid()).Should().Equal(c.Corridos[0], c.Corridos[2]);

        // Desfazer o W.O. (G12) devolve o jogo à lista, na mesma data, hora e local.
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/jogos/{c.Corridos[1]}/desfazer-wo", new { motivo = "engano" })).Status.Should().Be(HttpStatusCode.NoContent);
        (await DoArbitro(c)).Select(j => j.GetProperty("id").GetGuid()).Should().Equal(c.Corridos[0], c.Corridos[1], c.Corridos[2]);
    }
}
