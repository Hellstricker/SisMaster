using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// G10 pela API de verdade: detalhe da fase (prévia e tabela gerada), escolha manual dos grupos, cruzamentos do mata-mata
/// e acesso por perfil. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class FaseDetalheG10ApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public FaseDetalheG10ApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private static async Task<(HttpStatusCode Status, string Corpo)> Chamar(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private sealed record Cenario(HttpClient Admin, Guid AssociacaoId, Guid TemporadaId, Guid Tc, List<Guid> Equipes);

    /// <summary>Temporada com inscrições encerradas e 4 equipes (Águias, Ursos, Lobos, Tigres), sem nenhuma fase.</summary>
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
        await send(HttpMethod.Post, $"/api/associacoes/{assoc}/campeonatos", new { nome = "Campeonato" });
        var campeonato = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}", null)).GetProperty("campeonatos").EnumerateArray().First().GetProperty("id").GetGuid();

        var hoje = DateTime.UtcNow;
        await send(HttpMethod.Post, $"/api/campeonatos/{campeonato}/temporadas", new { ano = hoje.Year, dataInicioInscricoes = hoje.AddDays(-1), dataFimInscricoes = hoje.AddDays(30) });
        var temporada = (await send(HttpMethod.Get, $"/api/campeonatos/{campeonato}", null)).GetProperty("temporadas").EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/categorias", new { categoriaId = categoria });
        var tc = (await send(HttpMethod.Get, $"/api/temporadas/{temporada}", null)).GetProperty("categorias").EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Patch, $"/api/campeonatos/{campeonato}/temporadas/{temporada}/encerrar-inscricoes", new { });
        foreach (var equipe in new[] { "Águias", "Ursos", "Lobos", "Tigres" })
            await send(HttpMethod.Post, $"/api/temporadas/{temporada}/equipes", new { temporadaId = temporada, temporadaCategoriaId = tc, nome = equipe, cor = "#ff0000" });

        var cat = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{temporada}/equipes")).Corpo).RootElement;
        var equipes = (cat.TryGetProperty("data", out var d) ? d : cat).GetProperty("categorias")[0].GetProperty("equipes").EnumerateArray()
            .OrderBy(e => e.GetProperty("nome").GetString(), StringComparer.Ordinal).Select(e => e.GetProperty("id").GetGuid()).ToList();
        return new Cenario(admin, assoc, temporada, tc, equipes);
    }

    private static async Task<Guid> CriarFase(Cenario c, object corpo)
    {
        async Task<HashSet<Guid>> Ids()
        {
            var corpoJson = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/fases")).Corpo).RootElement;
            var raiz = corpoJson.TryGetProperty("data", out var d) ? d : corpoJson;
            return raiz.GetProperty("categorias")[0].GetProperty("fases").EnumerateArray().Select(f => f.GetProperty("id").GetGuid()).ToHashSet();
        }
        var antes = await Ids();
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases", corpo)).Status.Should().Be(HttpStatusCode.NoContent);
        return (await Ids()).First(id => !antes.Contains(id));
    }

    private static async Task<JsonElement> Detalhe(HttpClient admin, Guid fase)
    {
        var corpo = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/fases/{fase}")).Corpo).RootElement;
        return (corpo.TryGetProperty("data", out var d) ? d : corpo).Clone();
    }

    private static object Grupos(Cenario c, string nome, string distribuicao) => new
    {
        temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome, tipo = "Grupos", numeroTurnos = 1, numeroGrupos = 2, distribuicao,
    };

    private static List<string> NomesDoGrupo(JsonElement detalhe, int ordem) =>
        detalhe.GetProperty("grupos").EnumerateArray().First(g => g.GetProperty("ordem").GetInt32() == ordem)
            .GetProperty("equipes").EnumerateArray().Select(v => v.GetProperty("texto").GetString()!).ToList();

    // ------------------------------------------------------------------ detalhe

    [Fact]
    public async Task Detalhe_de_pontos_corridos_mostra_a_previa_da_tabela_antes_de_gerar()
    {
        if (SemBanco) return;
        var c = await NovoAsync("DetPrevia");
        var fase = await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });

        var d = await Detalhe(c.Admin, fase);

        d.GetProperty("previa").GetBoolean().Should().BeTrue();
        d.GetProperty("vagas").GetArrayLength().Should().Be(4);
        d.GetProperty("totalJogos").GetInt32().Should().Be(6, "4 equipes, 1 turno: todos contra todos");
        d.GetProperty("jogos").EnumerateArray().Should().OnlyContain(j => j.GetProperty("id").ValueKind == JsonValueKind.Null, "na prévia os jogos ainda não existem");
        d.GetProperty("permiteEditarCruzamentos").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Depois_de_gerar_a_tabela_o_detalhe_traz_os_jogos_de_verdade_e_trava_a_edicao()
    {
        if (SemBanco) return;
        var c = await NovoAsync("DetGerada");
        var fase = await CriarFase(c, Grupos(c, "Grupos", "Manual"));
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/encerrar-cadastro", new { });
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/tabela-jogos", new { });

        var d = await Detalhe(c.Admin, fase);

        d.GetProperty("previa").GetBoolean().Should().BeFalse();
        d.GetProperty("permiteEditarCruzamentos").GetBoolean().Should().BeFalse();
        d.GetProperty("totalJogos").GetInt32().Should().Be(2, "2 grupos de 2 equipes, 1 turno");
        d.GetProperty("jogos").EnumerateArray().Should().OnlyContain(j => j.GetProperty("id").ValueKind == JsonValueKind.String);

        var recusa = await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{fase}/distribuicao-manual", new { grupos = new[] { 1, 2, 1, 2 } });
        recusa.Status.Should().Be(HttpStatusCode.BadRequest);
        recusa.Corpo.Should().Contain("tabela de jogos já foi gerada");
    }

    // ------------------------------------------------------------------ grupos

    [Fact]
    public async Task Escolhe_o_grupo_de_cada_equipe_e_a_escolha_aparece_no_detalhe()
    {
        if (SemBanco) return;
        var c = await NovoAsync("GruposManual");
        var fase = await CriarFase(c, Grupos(c, "Grupos", "Manual"));

        var padrao = await Detalhe(c.Admin, fase);
        padrao.GetProperty("distribuicaoManualDefinida").GetBoolean().Should().BeFalse();
        NomesDoGrupo(padrao, 1).Should().HaveCount(2, "sem escolha manual, as vagas entram em blocos seguidos");

        (await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{fase}/distribuicao-manual", new { grupos = new[] { 1, 2, 2, 1 } })).Status.Should().Be(HttpStatusCode.NoContent);

        var d = await Detalhe(c.Admin, fase);
        d.GetProperty("distribuicaoManualDefinida").GetBoolean().Should().BeTrue();
        var vagas = d.GetProperty("vagas").EnumerateArray().OrderBy(v => v.GetProperty("posicao").GetInt32()).ToList();
        vagas.Select(v => v.GetProperty("grupoOrdem").GetInt32()).Should().Equal(1, 2, 2, 1);
        NomesDoGrupo(d, 1).Should().HaveCount(2);
        NomesDoGrupo(d, 2).Should().HaveCount(2);

        // Mover uma equipe de grupo: a escolha é substituída por inteiro.
        (await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{fase}/distribuicao-manual", new { grupos = new[] { 2, 2, 1, 1 } })).Status.Should().Be(HttpStatusCode.NoContent);
        var depois = (await Detalhe(c.Admin, fase)).GetProperty("vagas").EnumerateArray().OrderBy(v => v.GetProperty("posicao").GetInt32()).ToList();
        depois.Select(v => v.GetProperty("grupoOrdem").GetInt32()).Should().Equal(2, 2, 1, 1);
    }

    [Fact]
    public async Task Recusa_grupo_com_menos_de_duas_equipes_grupo_inexistente_e_distribuicao_que_nao_e_manual()
    {
        if (SemBanco) return;
        var c = await NovoAsync("GruposRecusa");
        var manual = await CriarFase(c, Grupos(c, "Manual", "Manual"));
        var serpentina = await CriarFase(c, Grupos(c, "Serpentina", "Serpentina"));

        var umSo = await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{manual}/distribuicao-manual", new { grupos = new[] { 1, 1, 1, 2 } });
        umSo.Status.Should().Be(HttpStatusCode.BadRequest);
        umSo.Corpo.Should().Contain("grupo B precisa de ao menos 2 equipes");

        var fora = await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{manual}/distribuicao-manual", new { grupos = new[] { 1, 2, 3, 1 } });
        fora.Status.Should().Be(HttpStatusCode.BadRequest);
        fora.Corpo.Should().Contain("entre A e B");

        var naoManual = await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{serpentina}/distribuicao-manual", new { grupos = new[] { 1, 2, 2, 1 } });
        naoManual.Status.Should().Be(HttpStatusCode.BadRequest);
        naoManual.Corpo.Should().Contain("distribuição manual");

        (await Detalhe(c.Admin, manual)).GetProperty("distribuicaoManualDefinida").GetBoolean().Should().BeFalse("nada foi gravado");
    }

    [Fact]
    public async Task Mudar_a_estrutura_da_fase_descarta_a_escolha_manual_dos_grupos()
    {
        if (SemBanco) return;
        var c = await NovoAsync("GruposReset");
        var fase = await CriarFase(c, Grupos(c, "Grupos", "Manual"));
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Put, $"/api/fases/{fase}/distribuicao-manual", new { grupos = new[] { 1, 2, 2, 1 } });

        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/fases/{fase}",
            new { nome = "Grupos", tipo = "Grupos", numeroTurnos = 2, numeroGrupos = 2, distribuicao = "Manual" })).Status.Should().Be(HttpStatusCode.NoContent);

        (await Detalhe(c.Admin, fase)).GetProperty("distribuicaoManualDefinida").GetBoolean().Should().BeFalse();
    }

    // ------------------------------------------------------------------ cruzamentos

    private static object Colocacao(Guid fase, int posicao) => new { tipo = "Colocacao", faseId = fase, posicao };

    [Fact]
    public async Task Define_e_substitui_cruzamentos_do_mata_mata_e_ele_aparece_no_detalhe()
    {
        if (SemBanco) return;
        var c = await NovoAsync("Cruzamento");
        var corridos = await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        var mata = await CriarFase(c, new
        {
            temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Semifinais", tipo = "MataMata", jogosPorConfronto = 3, numeroConfrontos = 2, faseAnteriorId = corridos,
        });

        var antes = await Detalhe(c.Admin, mata);
        antes.GetProperty("confrontos").GetArrayLength().Should().Be(0);
        antes.GetProperty("confrontosPendentes").EnumerateArray().Select(x => x.GetInt32()).Should().Equal(1, 2);

        (await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{mata}/confrontos/1", new { nome = "Semi 1", origemA = Colocacao(corridos, 1), origemB = Colocacao(corridos, 4) }))
            .Status.Should().Be(HttpStatusCode.NoContent);
        (await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{mata}/confrontos/2", new { nome = "Semi 2", origemA = Colocacao(corridos, 2), origemB = Colocacao(corridos, 3) }))
            .Status.Should().Be(HttpStatusCode.NoContent);

        var d = await Detalhe(c.Admin, mata);
        d.GetProperty("confrontosPendentes").GetArrayLength().Should().Be(0);
        var primeiro = d.GetProperty("confrontos").EnumerateArray().First(x => x.GetProperty("numero").GetInt32() == 1);
        primeiro.GetProperty("nome").GetString().Should().Be("Semi 1");
        primeiro.GetProperty("jogos").GetArrayLength().Should().Be(3, "melhor de 3");

        // Cruzar de novo substitui o confronto em vez de duplicar.
        (await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{mata}/confrontos/1", new { nome = "Semi A", origemA = Colocacao(corridos, 1), origemB = Colocacao(corridos, 3) }))
            .Status.Should().Be(HttpStatusCode.NoContent);
        var depois = await Detalhe(c.Admin, mata);
        depois.GetProperty("confrontos").GetArrayLength().Should().Be(2);
        depois.GetProperty("confrontos").EnumerateArray().First(x => x.GetProperty("numero").GetInt32() == 1).GetProperty("nome").GetString().Should().Be("Semi A");
    }

    [Fact]
    public async Task Recusa_cruzamento_invalido()
    {
        if (SemBanco) return;
        var c = await NovoAsync("CruzRecusa");
        var corridos = await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        var mata = await CriarFase(c, new
        {
            temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Final", tipo = "MataMata", jogosPorConfronto = 1, numeroConfrontos = 1, faseAnteriorId = corridos,
        });

        var demais = await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{mata}/confrontos/2", new { origemA = Colocacao(corridos, 1), origemB = Colocacao(corridos, 2) });
        demais.Status.Should().Be(HttpStatusCode.BadRequest);
        demais.Corpo.Should().Contain("1 confronto");

        var emFaseQueNaoEMataMata = await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{corridos}/confrontos/1", new { origemA = Colocacao(corridos, 1), origemB = Colocacao(corridos, 2) });
        emFaseQueNaoEMataMata.Status.Should().Be(HttpStatusCode.BadRequest);
        emFaseQueNaoEMataMata.Corpo.Should().Contain("mata-mata");

        var colocacaoDaPropriaFase = await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{mata}/confrontos/1", new { origemA = Colocacao(mata, 1), origemB = Colocacao(corridos, 2) });
        colocacaoDaPropriaFase.Status.Should().Be(HttpStatusCode.BadRequest);

        var equipeDeOutraCategoria = await Chamar(c.Admin, HttpMethod.Put, $"/api/fases/{mata}/confrontos/1",
            new { origemA = new { tipo = "Equipe", equipeId = Guid.NewGuid() }, origemB = Colocacao(corridos, 2) });
        equipeDeOutraCategoria.Status.Should().Be(HttpStatusCode.BadRequest);

        (await Detalhe(c.Admin, mata)).GetProperty("confrontos").GetArrayLength().Should().Be(0);
    }

    // ------------------------------------------------------------------ acesso

    [Fact]
    public async Task Arbitro_e_administrador_de_outra_associacao_nao_alteram_grupos_nem_cruzamentos()
    {
        if (SemBanco) return;
        var c = await NovoAsync("DetAcesso");
        var grupos = await CriarFase(c, Grupos(c, "Grupos", "Manual"));
        var corridos = await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        var mata = await CriarFase(c, new
        {
            temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Final", tipo = "MataMata", jogosPorConfronto = 1, numeroConfrontos = 1, faseAnteriorId = corridos,
        });
        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", c.AssociacaoId));
        var alheio = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", Guid.NewGuid()));

        foreach (var cliente in new[] { arbitro, alheio })
        {
            (await Chamar(cliente, HttpMethod.Get, $"/api/fases/{grupos}")).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Chamar(cliente, HttpMethod.Put, $"/api/fases/{grupos}/distribuicao-manual", new { grupos = new[] { 1, 2, 2, 1 } })).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Chamar(cliente, HttpMethod.Put, $"/api/fases/{mata}/confrontos/1", new { origemA = Colocacao(corridos, 1), origemB = Colocacao(corridos, 2) })).Status.Should().Be(HttpStatusCode.Forbidden);
        }

        (await Detalhe(c.Admin, grupos)).GetProperty("distribuicaoManualDefinida").GetBoolean().Should().BeFalse();
        (await Detalhe(c.Admin, mata)).GetProperty("confrontos").GetArrayLength().Should().Be(0);
    }
}
