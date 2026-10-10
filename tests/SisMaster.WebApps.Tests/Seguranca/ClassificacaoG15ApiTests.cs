using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// G15 pela API de verdade: a classificação calculada dos jogos (pontos de vitória, derrota e W.O.), o empate sem critério
/// resolvido por sorteio da diretoria (que destrava a fase seguinte) e a série do mata-mata. Os resultados saem de W.O.
/// (20 × 0), que são determinísticos. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class ClassificacaoG15ApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public ClassificacaoG15ApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private static async Task<(HttpStatusCode Status, string Corpo)> Chamar(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private sealed record Cenario(HttpClient Admin, Guid AssociacaoId, Guid TemporadaId, Guid Tc, Dictionary<string, Guid> Equipes, Guid Corridos, Guid Final);

    /// <summary>Águias, Ursos e Lobos em pontos corridos (1 turno) e uma final de 1 jogo entre o 1º e o 2º colocados.</summary>
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
        var json = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{temporada}/equipes")).Corpo).RootElement;
        var equipes = (json.TryGetProperty("data", out var d) ? d : json).GetProperty("categorias")[0].GetProperty("equipes").EnumerateArray()
            .ToDictionary(x => x.GetProperty("nome").GetString()!, x => x.GetProperty("id").GetGuid());

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
        var corridos = await Fase(new { temporadaId = temporada, temporadaCategoriaId = tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1, classificadosPrimeiros = 2 });
        var final = await Fase(new { temporadaId = temporada, temporadaCategoriaId = tc, nome = "Final", tipo = "MataMata", jogosPorConfronto = 1, numeroConfrontos = 1, faseAnteriorId = corridos });
        await send(HttpMethod.Put, $"/api/fases/{final}/confrontos/1", new
        {
            nome = "Final",
            origemA = new { tipo = "Colocacao", faseId = corridos, posicao = 1 },
            origemB = new { tipo = "Colocacao", faseId = corridos, posicao = 2 },
        });
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/fases/encerrar-cadastro", new { });
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/tabela-jogos", new { });
        return new Cenario(admin, assoc, temporada, tc, equipes, corridos, final);
    }

    private sealed record JogoDaTabela(Guid Id, string Casa, string Visitante, string Fase, string Status, bool CasaDefinida);

    private static async Task<List<JogoDaTabela>> Jogos(Cenario c)
    {
        var json = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/jogos")).Corpo).RootElement;
        return json.GetProperty("jogos").EnumerateArray().OrderBy(j => j.GetProperty("numero").GetInt32())
            .Select(j => new JogoDaTabela(j.GetProperty("id").GetGuid(), j.GetProperty("casa").GetProperty("texto").GetString()!,
                j.GetProperty("visitante").GetProperty("texto").GetString()!, j.GetProperty("fase").GetProperty("nome").GetString()!,
                j.GetProperty("status").GetString()!, j.GetProperty("casa").GetProperty("definida").GetBoolean())).ToList();
    }

    /// <summary>O jogo entre as duas equipes termina em W.O. de quem perde: o vencedor leva 20 × 0.</summary>
    private static async Task Vence(Cenario c, string vencedor, string perdedor)
    {
        var jogo = (await Jogos(c)).First(j => j.Fase == "Pontos" && new[] { j.Casa, j.Visitante }.Contains(vencedor) && new[] { j.Casa, j.Visitante }.Contains(perdedor));
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/jogos/{jogo.Id}/wo", new { casaAusente = jogo.Casa == perdedor })).Status.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<JsonElement> Classificacao(Cenario c, Guid fase) =>
        JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/fases/{fase}/classificacao")).Corpo).RootElement.GetProperty("classificacao").Clone();

    private static List<string> Ordem(JsonElement classificacao) =>
        classificacao.GetProperty("linhas").EnumerateArray().Select(l => l.GetProperty("texto").GetString()!).ToList();

    private static Task<(HttpStatusCode Status, string Corpo)> Sortear(Cenario c, Guid fase, IEnumerable<Guid> vagas) =>
        Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{fase}/sorteio", new { ordem = vagas });

    // ------------------------------------------------------------------ pontos

    [Fact]
    public async Task Pontos_de_vitoria_derrota_e_wo_somam_no_total_e_ordenam_a_tabela()
    {
        if (SemBanco) return;
        var c = await NovoAsync("ClasPontos");
        await Vence(c, "Águias", "Ursos");   // Águias 2, Ursos 0 (derrota por W.O.)
        await Vence(c, "Águias", "Lobos");   // Águias 4, Lobos 0

        var t = await Classificacao(c, c.Corridos);

        t.GetProperty("definitiva").GetBoolean().Should().BeFalse("falta o jogo Ursos × Lobos");
        var linhas = t.GetProperty("linhas").EnumerateArray().ToDictionary(l => l.GetProperty("texto").GetString()!);
        Ordem(t).First().Should().Be("Águias");
        linhas["Águias"].GetProperty("total").GetDecimal().Should().Be(4);
        linhas["Águias"].GetProperty("vitorias").GetInt32().Should().Be(2);
        linhas["Águias"].GetProperty("posicao").GetInt32().Should().Be(1);
        linhas["Ursos"].GetProperty("derrotasWO").GetInt32().Should().Be(1);
        linhas["Ursos"].GetProperty("total").GetDecimal().Should().Be(0, "derrota por W.O. não pontua");
        linhas["Águias"].GetProperty("pontosFeitos").GetInt32().Should().Be(40);
        linhas["Ursos"].GetProperty("pontosSofridos").GetInt32().Should().Be(20);
        t.GetProperty("podeDefinirSorteio").GetBoolean().Should().BeFalse();
    }

    // ------------------------------------------------------------------ sorteio

    [Fact]
    public async Task Empate_sem_criterio_so_vira_sorteio_com_a_tabela_terminada_e_o_sorteio_destrava_a_fase_seguinte()
    {
        if (SemBanco) return;
        var c = await NovoAsync("ClasSorteio");
        await Vence(c, "Águias", "Ursos");
        await Vence(c, "Ursos", "Lobos");

        var antes = await Sortear(c, c.Corridos, c.Equipes.Values);
        antes.Status.Should().Be(HttpStatusCode.BadRequest);
        antes.Corpo.Should().Contain("depois de terminados todos os jogos");

        await Vence(c, "Lobos", "Águias");   // ciclo: cada um vence um e perde um por W.O.

        var t = await Classificacao(c, c.Corridos);
        t.GetProperty("definitiva").GetBoolean().Should().BeTrue();
        t.GetProperty("podeDefinirSorteio").GetBoolean().Should().BeTrue();
        t.GetProperty("sorteioDefinido").GetBoolean().Should().BeFalse();
        var linhas = t.GetProperty("linhas").EnumerateArray().ToList();
        linhas.Should().OnlyContain(l => l.GetProperty("empatePorSorteio").GetBoolean(), "pontos, saldo e confronto direto empatam nas três");
        linhas.Should().OnlyContain(l => l.GetProperty("total").GetDecimal() == 2);
        (await Jogos(c)).First(j => j.Fase == "Final").CasaDefinida.Should().BeFalse("a final espera o sorteio do 1º e do 2º");

        var vagas = linhas.ToDictionary(l => l.GetProperty("texto").GetString()!, l => l.GetProperty("vagaId").GetGuid());

        var incompleta = await Sortear(c, c.Corridos, [vagas["Lobos"], vagas["Águias"]]);
        incompleta.Status.Should().Be(HttpStatusCode.BadRequest);
        incompleta.Corpo.Should().Contain("exatamente as equipes empatadas");
        (await Sortear(c, c.Corridos, [vagas["Lobos"], vagas["Lobos"], vagas["Ursos"]])).Status.Should().Be(HttpStatusCode.BadRequest);

        (await Sortear(c, c.Corridos, [vagas["Lobos"], vagas["Águias"], vagas["Ursos"]])).Status.Should().Be(HttpStatusCode.NoContent);

        var depois = await Classificacao(c, c.Corridos);
        Ordem(depois).Should().Equal("Lobos", "Águias", "Ursos");
        depois.GetProperty("sorteioDefinido").GetBoolean().Should().BeTrue();
        var final = (await Jogos(c)).First(j => j.Fase == "Final");
        final.Casa.Should().Be("Lobos", "o 1º do sorteio");
        final.Visitante.Should().Be("Águias", "o 2º do sorteio");

        var alterar = await Sortear(c, c.Corridos, [vagas["Águias"], vagas["Lobos"], vagas["Ursos"]]);
        alterar.Status.Should().Be(HttpStatusCode.BadRequest, "a final já foi definida a partir deste sorteio");
        alterar.Corpo.Should().Contain("não pode mais ser alterado");
    }

    [Fact]
    public async Task Sem_empate_nao_ha_sorteio_e_mata_mata_nao_tem_sorteio()
    {
        if (SemBanco) return;
        var c = await NovoAsync("ClasSemEmpate");
        await Vence(c, "Águias", "Ursos");
        await Vence(c, "Águias", "Lobos");
        await Vence(c, "Ursos", "Lobos");
        var vagas = (await Classificacao(c, c.Corridos)).GetProperty("linhas").EnumerateArray().Select(l => l.GetProperty("vagaId").GetGuid()).ToList();

        var semEmpate = await Sortear(c, c.Corridos, vagas.Take(2));
        semEmpate.Status.Should().Be(HttpStatusCode.BadRequest);
        semEmpate.Corpo.Should().Contain("Não há empate sem critério");

        var mata = await Sortear(c, c.Final, vagas.Take(2));
        mata.Status.Should().Be(HttpStatusCode.BadRequest);
        mata.Corpo.Should().Contain("Mata-mata não tem tabela");

        Ordem(await Classificacao(c, c.Corridos)).Should().Equal("Águias", "Ursos", "Lobos");
    }

    // ------------------------------------------------------------------ mata-mata

    [Fact]
    public async Task Serie_do_mata_mata_mostra_as_vitorias_e_o_vencedor()
    {
        if (SemBanco) return;
        var c = await NovoAsync("ClasSerie");
        await Vence(c, "Águias", "Ursos");
        await Vence(c, "Águias", "Lobos");
        await Vence(c, "Ursos", "Lobos");   // Águias 1º, Ursos 2º

        var final = (await Jogos(c)).First(j => j.Fase == "Final");
        final.Casa.Should().Be("Águias");
        final.Visitante.Should().Be("Ursos");
        var antes = (await Chamar(c.Admin, HttpMethod.Get, $"/api/fases/{c.Final}/classificacao")).Corpo;
        var confrontoAntes = JsonDocument.Parse(antes).RootElement.GetProperty("classificacao").GetProperty("confrontos")[0];
        confrontoAntes.GetProperty("decidido").GetBoolean().Should().BeFalse();

        (await Chamar(c.Admin, HttpMethod.Post, $"/api/jogos/{final.Id}/wo", new { casaAusente = true })).Status.Should().Be(HttpStatusCode.NoContent); // as Águias (casa) faltam: os Ursos (visitante) vencem

        var m = await Classificacao(c, c.Final);
        m.GetProperty("tipo").GetString().Should().Be("MataMata");
        m.GetProperty("vitoriasNecessarias").GetInt32().Should().Be(1);
        var confronto = m.GetProperty("confrontos")[0];
        confronto.GetProperty("decidido").GetBoolean().Should().BeTrue();
        confronto.GetProperty("b").GetProperty("venceu").GetBoolean().Should().BeTrue();
        confronto.GetProperty("a").GetProperty("venceu").GetBoolean().Should().BeFalse();
        confronto.GetProperty("b").GetProperty("equipe").GetProperty("nome").GetString().Should().Be("Ursos");
    }

    // ------------------------------------------------------------------ acesso

    [Fact]
    public async Task Arbitro_e_administrador_de_outra_associacao_nao_veem_a_classificacao_nem_registram_sorteio()
    {
        if (SemBanco) return;
        var c = await NovoAsync("ClasAcesso");
        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", c.AssociacaoId));
        var alheio = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", Guid.NewGuid()));

        foreach (var cliente in new[] { arbitro, alheio })
        {
            (await Chamar(cliente, HttpMethod.Get, $"/api/fases/{c.Corridos}/classificacao")).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Chamar(cliente, HttpMethod.Put, $"/api/fases/{c.Corridos}/sorteio", new { ordem = c.Equipes.Values })).Status.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
