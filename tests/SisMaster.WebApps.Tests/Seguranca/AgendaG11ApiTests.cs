using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// G11 pela API de verdade: conflitos de agenda (local e equipe bloqueiam, atleta só avisa), no agendamento de um jogo, em lote
/// (tudo ou nada) e na conferência prévia. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class AgendaG11ApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private const string Dia = "2026-11-08";
    private readonly ApiFactory _fabrica;

    public AgendaG11ApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private static async Task<(HttpStatusCode Status, string Corpo)> Chamar(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private static string CpfValido(int n)
    {
        var b = (100000000 + n * 7919 % 800000000).ToString()[..9].Select(c => c - '0').ToArray();
        int Dv(int[] a) { var s = a.Select((x, i) => x * (a.Length + 1 - i)).Sum(); var r = s * 10 % 11; return r == 10 ? 0 : r; }
        var d1 = Dv(b);
        var d2 = Dv([.. b, d1]);
        return string.Concat(b) + d1 + d2;
    }

    /// <summary>
    /// Categoria 1 (Master 40+) com Águias, Ursos e Lobos: 3 jogos, todos dois a dois com uma equipe em comum.
    /// Categoria 2 (Supermaster 50+) com Falcões e Corujas: 1 jogo. O atleta "Ozias Duplo" está nas Águias e nos Falcões.
    /// </summary>
    private sealed record Cenario(HttpClient Admin, Guid AssociacaoId, Guid TemporadaId, Guid LocalA, Guid LocalB,
        Guid AguiasUrsos, Guid AguiasLobos, Guid UrsosLobos, Guid FalcoesCorujas);

    private async Task<Cenario> NovoAsync(string nome)
    {
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var send = (HttpMethod m, string u, object? b) => DadosDeTeste.Enviar(admin, m, u, b);

        await send(HttpMethod.Post, "/api/associacoes", new { nome = $"Associação {nome}", sigla = nome.Length > 8 ? nome[..8] : nome, uf = "BA" });
        var assoc = (await send(HttpMethod.Get, "/api/associacoes", null)).EnumerateArray()
            .First(a => a.GetProperty("nome").GetString() == $"Associação {nome}").GetProperty("id").GetGuid();
        foreach (var (n, idade) in new[] { ("Master 40+", 40), ("Supermaster 50+", 50) })
            await send(HttpMethod.Post, $"/api/associacoes/{assoc}/categorias",
                new { nome = n, idadeMinima = idade, sexo = "Masculino", aceitaAbaixoIdadeMinima = false, minimoPeriodosEmQuadra = 1, minimoPeriodosForaQuadra = 1 });
        var categorias = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}/categorias", null)).EnumerateArray().ToList();
        Guid Cat(string n) => categorias.First(c => c.GetProperty("nome").GetString() == n).GetProperty("id").GetGuid();

        foreach (var l in new[] { "Ginásio A", "Ginásio B" })
            await send(HttpMethod.Post, $"/api/associacoes/{assoc}/locais", new { nome = l, cidade = "Salvador", estado = "BA" });
        var locais = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}/locais", null)).EnumerateArray().ToList();
        Guid Local(string n) => locais.First(l => l.GetProperty("nome").GetString() == n).GetProperty("id").GetGuid();

        await send(HttpMethod.Post, $"/api/associacoes/{assoc}/campeonatos", new { nome = "Campeonato" });
        var campeonato = (await send(HttpMethod.Get, $"/api/associacoes/{assoc}", null)).GetProperty("campeonatos").EnumerateArray().First().GetProperty("id").GetGuid();
        var hoje = DateTime.UtcNow;
        await send(HttpMethod.Post, $"/api/campeonatos/{campeonato}/temporadas", new { ano = hoje.Year, dataInicioInscricoes = hoje.AddDays(-1), dataFimInscricoes = hoje.AddDays(30) });
        var temporada = (await send(HttpMethod.Get, $"/api/campeonatos/{campeonato}", null)).GetProperty("temporadas").EnumerateArray().First().GetProperty("id").GetGuid();
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/categorias", new { categoriaId = Cat("Master 40+") });
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/categorias", new { categoriaId = Cat("Supermaster 50+") });
        var tcs = (await send(HttpMethod.Get, $"/api/temporadas/{temporada}", null)).GetProperty("categorias").EnumerateArray().ToList();
        Guid Tc(string n) => tcs.First(c => c.GetProperty("nome").GetString() == n).GetProperty("id").GetGuid();
        var tc1 = Tc("Master 40+");
        var tc2 = Tc("Supermaster 50+");

        await Chamar(admin, HttpMethod.Patch, $"/api/temporadas/{temporada}/taxa-inscricao", new { taxa = 100 });
        await send(HttpMethod.Patch, $"/api/campeonatos/{campeonato}/temporadas/{temporada}/encerrar-inscricoes", new { });

        async Task<Guid> Equipe(Guid tc, string n)
        {
            await send(HttpMethod.Post, $"/api/temporadas/{temporada}/equipes", new { temporadaId = temporada, temporadaCategoriaId = tc, nome = n, cor = "#ff0000" });
            var corpo = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{temporada}/equipes")).Corpo).RootElement;
            return (corpo.TryGetProperty("data", out var d) ? d : corpo).GetProperty("categorias").EnumerateArray()
                .SelectMany(c => c.GetProperty("equipes").EnumerateArray()).First(e => e.GetProperty("nome").GetString() == n).GetProperty("id").GetGuid();
        }
        var aguias = await Equipe(tc1, "Águias");
        await Equipe(tc1, "Ursos");
        await Equipe(tc1, "Lobos");
        var falcoes = await Equipe(tc2, "Falcões");
        await Equipe(tc2, "Corujas");

        // O atleta que joga as duas categorias: uma ficha com dois pedidos, aprovados e com a taxa paga.
        await send(HttpMethod.Post, "/api/inscricoes", new
        {
            temporadaId = temporada, nome = "Ozias Duplo", cpf = CpfValido(80000), nascimento = "1970-05-10", sexo = "Masculino", email = "duplo@exemplo.test",
            telefone = "71999990000", alturaCm = 180, pesoKg = 80, posicao = "Ala", consentimentoLgpd = true, temporadaCategoriaIds = new[] { tc1, tc2 },
        });
        var ficha = (await send(HttpMethod.Get, $"/api/temporadas/{temporada}/inscricoes", null)).EnumerateArray()
            .First(f => f.GetProperty("pessoa").GetProperty("nome").GetString() == "Ozias Duplo");
        foreach (var pedido in ficha.GetProperty("categorias").EnumerateArray())
            await send(HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido.GetProperty("id").GetGuid()}/aprovar", new { });
        await send(HttpMethod.Post, $"/api/inscricoes/{ficha.GetProperty("id").GetGuid()}/pagamentos/taxa", new { dataPagamento = "2026-10-09" });
        foreach (var pedido in ficha.GetProperty("categorias").EnumerateArray())
        {
            var daCategoria = pedido.GetProperty("temporadaCategoriaId").GetGuid();
            await send(HttpMethod.Post, $"/api/equipes/{(daCategoria == tc1 ? aguias : falcoes)}/atletas", new { inscricaoCategoriaId = pedido.GetProperty("id").GetGuid() });
        }

        foreach (var tc in new[] { tc1, tc2 })
            await send(HttpMethod.Post, $"/api/temporadas/{temporada}/fases", new { temporadaId = temporada, temporadaCategoriaId = tc, nome = "Fase única", tipo = "PontosCorridos", numeroTurnos = 1 });
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/fases/encerrar-cadastro", new { });
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/tabela-jogos", new { });

        var lista = (await send(HttpMethod.Get, $"/api/temporadas/{temporada}/jogos", null)).GetProperty("jogos").EnumerateArray().ToList();
        Guid Jogo(string a, string b) => lista.First(j =>
        {
            var casa = j.GetProperty("casa").GetProperty("texto").GetString();
            var vis = j.GetProperty("visitante").GetProperty("texto").GetString();
            return (casa == a && vis == b) || (casa == b && vis == a);
        }).GetProperty("id").GetGuid();

        return new Cenario(admin, assoc, temporada, Local("Ginásio A"), Local("Ginásio B"),
            Jogo("Águias", "Ursos"), Jogo("Águias", "Lobos"), Jogo("Ursos", "Lobos"), Jogo("Falcões", "Corujas"));
    }

    private static Task<(HttpStatusCode Status, string Corpo)> Agendar(Cenario c, Guid jogo, string hora, Guid? local, string data = Dia) =>
        Chamar(c.Admin, HttpMethod.Patch, $"/api/jogos/{jogo}", new { data, hora = $"{hora}:00", localId = local });

    private static async Task<JsonElement> Jogo(Cenario c, Guid id) =>
        JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/jogos")).Corpo).RootElement
            .Let(r => r.TryGetProperty("data", out var d) ? d : r)
            .GetProperty("jogos").EnumerateArray().First(j => j.GetProperty("id").GetGuid() == id).Clone();

    // ------------------------------------------------------------------ bloqueios

    [Fact]
    public async Task Mesmo_local_com_menos_de_90_minutos_e_recusado_e_com_90_e_aceito()
    {
        if (SemBanco) return;
        var c = await NovoAsync("AgLocal");
        (await Agendar(c, c.AguiasUrsos, "10:00", c.LocalA)).Status.Should().Be(HttpStatusCode.NoContent);

        var perto = await Agendar(c, c.FalcoesCorujas, "10:30", c.LocalA);
        perto.Status.Should().Be(HttpStatusCode.BadRequest);
        perto.Corpo.Should().Contain("Local ocupado").And.Contain("Ginásio A");
        (await Jogo(c, c.FalcoesCorujas)).GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null, "nada foi gravado");

        (await Agendar(c, c.FalcoesCorujas, "10:30", c.LocalB)).Status.Should().Be(HttpStatusCode.NoContent);
        var ursos = await Agendar(c, c.UrsosLobos, "11:00", c.LocalB);
        ursos.Status.Should().Be(HttpStatusCode.BadRequest);
        ursos.Corpo.Should().Contain("Ursos já joga");
        (await Agendar(c, c.UrsosLobos, "11:30", c.LocalA)).Status.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_mesma_equipe_em_dois_jogos_proximos_e_recusada_mesmo_em_outro_local()
    {
        if (SemBanco) return;
        var c = await NovoAsync("AgEquipe");
        (await Agendar(c, c.AguiasUrsos, "10:00", c.LocalA)).Status.Should().Be(HttpStatusCode.NoContent);

        var r = await Agendar(c, c.AguiasLobos, "11:00", c.LocalB);

        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Corpo.Should().Contain("Águias já joga");
        (await Agendar(c, c.AguiasLobos, "11:30", c.LocalB)).Status.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Reagendar_o_mesmo_jogo_no_mesmo_horario_nao_conflita_com_ele_mesmo()
    {
        if (SemBanco) return;
        var c = await NovoAsync("AgMesmo");
        (await Agendar(c, c.AguiasUrsos, "10:00", c.LocalA)).Status.Should().Be(HttpStatusCode.NoContent);

        (await Agendar(c, c.AguiasUrsos, "10:00", c.LocalA)).Status.Should().Be(HttpStatusCode.NoContent);
        (await Agendar(c, c.AguiasUrsos, "10:30", c.LocalA)).Status.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Em_lote_o_conflito_recusa_tudo_e_sem_conflito_agenda_todos()
    {
        if (SemBanco) return;
        var c = await NovoAsync("AgLote");
        var lote = (int intervalo) => Chamar(c.Admin, HttpMethod.Patch, $"/api/temporadas/{c.TemporadaId}/jogos/agendar-lote",
            new { jogoIds = new[] { c.AguiasUrsos, c.FalcoesCorujas, c.UrsosLobos }, data = Dia, horaInicial = "09:00:00", intervaloMinutos = intervalo, localId = c.LocalA });

        var curto = await lote(60);
        curto.Status.Should().Be(HttpStatusCode.BadRequest);
        curto.Corpo.Should().Contain("Local ocupado");
        foreach (var id in new[] { c.AguiasUrsos, c.FalcoesCorujas, c.UrsosLobos })
            (await Jogo(c, id)).GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null, "tudo ou nada");

        // 90 min no mesmo local: o 1º e o 3º jogo compartilham o Ursos, mas ficam 180 min afastados.
        (await lote(90)).Status.Should().Be(HttpStatusCode.NoContent);
        var horas = new List<string?>();
        foreach (var id in new[] { c.AguiasUrsos, c.FalcoesCorujas, c.UrsosLobos })
            horas.Add((await Jogo(c, id)).GetProperty("hora").GetString());
        horas.Should().Equal("09:00:00", "10:30:00", "12:00:00");
    }

    // ------------------------------------------------------------------ conferência prévia e aviso de atleta

    [Fact]
    public async Task Conferir_agenda_mostra_bloqueios_e_avisos_sem_gravar()
    {
        if (SemBanco) return;
        var c = await NovoAsync("AgConferir");
        (await Agendar(c, c.AguiasUrsos, "10:00", c.LocalA)).Status.Should().Be(HttpStatusCode.NoContent);

        var conferir = async (Guid jogo, string hora, Guid local) =>
        {
            var r = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/jogos/conferir-agenda",
                new { itens = new[] { new { jogoId = jogo, data = Dia, hora = $"{hora}:00", localId = local } } });
            r.Status.Should().Be(HttpStatusCode.OK);
            var corpo = JsonDocument.Parse(r.Corpo).RootElement;
            return (corpo.TryGetProperty("data", out var d) ? d : corpo).Clone();
        };

        var bloqueado = await conferir(c.AguiasLobos, "10:45", c.LocalB);
        bloqueado.GetProperty("ok").GetBoolean().Should().BeFalse();
        bloqueado.GetProperty("bloqueios")[0].GetString().Should().Contain("Águias já joga");

        // O mesmo atleta (Ozias Duplo) está nas Águias e nos Falcões: só avisa, e depois deixa salvar.
        var aviso = await conferir(c.FalcoesCorujas, "10:30", c.LocalB);
        aviso.GetProperty("ok").GetBoolean().Should().BeTrue();
        aviso.GetProperty("bloqueios").GetArrayLength().Should().Be(0);
        aviso.GetProperty("avisos")[0].GetString().Should().Contain("Ozias Duplo").And.Contain("Águias").And.Contain("Falcões");
        (await Agendar(c, c.FalcoesCorujas, "10:30", c.LocalB)).Status.Should().Be(HttpStatusCode.NoContent);

        (await Jogo(c, c.AguiasLobos)).GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null, "conferir não grava");

        // Depois de agendados, a lista de jogos marca os dois com o aviso do atleta (e só eles).
        (await Jogo(c, c.FalcoesCorujas)).GetProperty("avisoAtleta").GetString().Should().Contain("Ozias Duplo");
        (await Jogo(c, c.AguiasUrsos)).GetProperty("avisoAtleta").GetString().Should().Contain("Ozias Duplo");
        (await Jogo(c, c.UrsosLobos)).GetProperty("avisoAtleta").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ------------------------------------------------------------------ acesso

    [Fact]
    public async Task Arbitro_e_administrador_de_outra_associacao_nao_conferem_nem_agendam()
    {
        if (SemBanco) return;
        var c = await NovoAsync("AgAcesso");
        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", c.AssociacaoId));
        var alheio = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", Guid.NewGuid()));
        var corpo = new { itens = new[] { new { jogoId = c.AguiasUrsos, data = Dia, hora = "10:00:00", localId = c.LocalA } } };

        foreach (var cliente in new[] { arbitro, alheio })
        {
            (await Chamar(cliente, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/jogos/conferir-agenda", corpo)).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Chamar(cliente, HttpMethod.Patch, $"/api/jogos/{c.AguiasUrsos}", new { data = Dia, hora = "10:00:00", localId = c.LocalA })).Status.Should().Be(HttpStatusCode.Forbidden);
        }
        (await Jogo(c, c.AguiasUrsos)).GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
    }
}

internal static class LetExtensao
{
    public static TOut Let<TIn, TOut>(this TIn valor, Func<TIn, TOut> f) => f(valor);
}
