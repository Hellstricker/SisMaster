using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SisMaster.WebApps.WebApi.Data;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// G12 pela API de verdade: registrar W.O. (20 × 0 para a outra equipe, com auditoria), desfazer W.O. (motivo obrigatório,
/// jogo volta a Agendado, classificação recalculada, jogo "se necessário" dispensado volta) e as recusas (nada calculado
/// a partir do resultado, súmula iniciada, jogo que não está em W.O., acesso por perfil).
/// Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class WoG12ApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public WoG12ApiTests(ApiFactory fabrica) => _fabrica = fabrica;

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

    private sealed record Cenario(HttpClient Admin, Guid AssociacaoId, Guid TemporadaId, Guid Tc, Dictionary<string, Guid> Equipes);

    /// <summary>Temporada com inscrições encerradas e as equipes informadas (todas da mesma categoria); ainda sem fases.</summary>
    private async Task<Cenario> NovoAsync(string nome, params string[] equipes)
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
        await Chamar(admin, HttpMethod.Patch, $"/api/temporadas/{temporada}/taxa-inscricao", new { taxa = 100 });
        await send(HttpMethod.Patch, $"/api/campeonatos/{campeonato}/temporadas/{temporada}/encerrar-inscricoes", new { });
        foreach (var e in equipes)
            await send(HttpMethod.Post, $"/api/temporadas/{temporada}/equipes", new { temporadaId = temporada, temporadaCategoriaId = tc, nome = e, cor = "#ff0000" });

        var corpo = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{temporada}/equipes")).Corpo).RootElement;
        var ids = (corpo.TryGetProperty("data", out var d) ? d : corpo).GetProperty("categorias")[0].GetProperty("equipes").EnumerateArray()
            .ToDictionary(x => x.GetProperty("nome").GetString()!, x => x.GetProperty("id").GetGuid());
        return new Cenario(admin, assoc, temporada, tc, ids);
    }

    private static async Task<Guid> CriarFase(Cenario c, object corpo)
    {
        async Task<HashSet<Guid>> Ids()
        {
            var json = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/fases")).Corpo).RootElement;
            return (json.TryGetProperty("data", out var d) ? d : json).GetProperty("categorias")[0].GetProperty("fases").EnumerateArray()
                .Select(f => f.GetProperty("id").GetGuid()).ToHashSet();
        }
        var antes = await Ids();
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases", corpo);
        return (await Ids()).First(id => !antes.Contains(id));
    }

    private static async Task GerarTabela(Cenario c)
    {
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/encerrar-cadastro", new { });
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/tabela-jogos", new { });
    }

    private sealed record JogoDaTabela(Guid Id, int Numero, string Casa, string Visitante, string Status, string Fase);

    private static async Task<List<JogoDaTabela>> Jogos(Cenario c)
    {
        var json = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/jogos")).Corpo).RootElement;
        return (json.TryGetProperty("data", out var d) ? d : json).GetProperty("jogos").EnumerateArray()
            .Select(j => new JogoDaTabela(j.GetProperty("id").GetGuid(), j.GetProperty("numero").GetInt32(),
                j.GetProperty("casa").GetProperty("texto").GetString()!, j.GetProperty("visitante").GetProperty("texto").GetString()!,
                j.GetProperty("status").GetString()!, j.GetProperty("fase").GetProperty("nome").GetString()!))
            .OrderBy(j => j.Numero).ToList();
    }

    private static async Task<JsonElement> Detalhe(Cenario c, Guid jogo)
    {
        // O detalhe do jogo tem a propriedade "data" (a data do jogo): a resposta é lida como veio, sem procurar envelope.
        return JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/jogos/{jogo}")).Corpo).RootElement.Clone();
    }

    private static Task<(HttpStatusCode Status, string Corpo)> RegistrarWO(Cenario c, Guid jogo, bool casaAusente) =>
        Chamar(c.Admin, HttpMethod.Post, $"/api/jogos/{jogo}/wo", new { casaAusente });

    private static Task<(HttpStatusCode Status, string Corpo)> Desfazer(Cenario c, Guid jogo, string motivo) =>
        Chamar(c.Admin, HttpMethod.Post, $"/api/jogos/{jogo}/desfazer-wo", new { motivo });

    private static async Task<int> TotalDePontos(Cenario c, Guid fase)
    {
        var json = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/fases/{fase}/classificacao")).Corpo).RootElement;
        var raiz = json.TryGetProperty("data", out var d) ? d : json;
        return raiz.GetProperty("classificacao").GetProperty("linhas").EnumerateArray().Sum(l => (int)l.GetProperty("total").GetDecimal());
    }

    // ------------------------------------------------------------------ registrar

    [Fact]
    public async Task Registrar_wo_da_20_a_0_para_a_outra_equipe_e_registra_no_historico()
    {
        if (SemBanco) return;
        var c = await NovoAsync("WoRegistrar", "Águias", "Ursos", "Lobos");
        await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        await GerarTabela(c);
        var jogo = (await Jogos(c))[0];

        (await RegistrarWO(c, jogo.Id, casaAusente: true)).Status.Should().Be(HttpStatusCode.NoContent);

        var d = await Detalhe(c, jogo.Id);
        d.GetProperty("status").GetString().Should().Be("WO");
        d.GetProperty("placarCasa").GetInt32().Should().Be(0);
        d.GetProperty("placarVisitante").GetInt32().Should().Be(20);
        d.GetProperty("woCasaAusente").GetBoolean().Should().BeTrue();
        d.GetProperty("podeDesfazerWO").GetBoolean().Should().BeTrue();
        var historico = d.GetProperty("historico").EnumerateArray().ToList();
        historico.Should().ContainSingle().Which.GetProperty("acao").GetString().Should().Be("RegistrarWO");
        historico[0].GetProperty("detalhe").GetString().Should().Contain(jogo.Casa).And.Contain("ausente").And.Contain("20 × 0");

        (await RegistrarWO(c, jogo.Id, casaAusente: false)).Status.Should().Be(HttpStatusCode.BadRequest, "o W.O. só vale para jogo agendado");
    }

    [Fact]
    public async Task Registrar_wo_descarta_a_sumula_so_preparada()
    {
        if (SemBanco) return;
        var c = await NovoAsync("WoSumula", "Águias", "Ursos");
        await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        await GerarTabela(c);
        var jogo = (await Jogos(c))[0];
        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", c.AssociacaoId));
        await DadosDeTeste.Enviar(arbitro, HttpMethod.Post, $"/api/jogos/{jogo.Id}/sumula");
        (await Detalhe(c, jogo.Id)).GetProperty("sumula").ValueKind.Should().Be(JsonValueKind.Object);

        (await RegistrarWO(c, jogo.Id, casaAusente: false)).Status.Should().Be(HttpStatusCode.NoContent);

        (await Detalhe(c, jogo.Id)).GetProperty("sumula").ValueKind.Should().Be(JsonValueKind.Null, "a súmula só preparada é descartada junto");
    }

    [Fact]
    public async Task Wo_e_recusado_com_a_sumula_ja_iniciada()
    {
        if (SemBanco) return;
        var c = await NovoAsync("WoIniciada", "Águias", "Ursos");
        await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        await GerarTabela(c);
        var jogo = (await Jogos(c))[0];

        // 4 atletas por equipe (o mínimo para iniciar), aprovados, com a taxa paga e no elenco.
        var seed = 91000;
        var porEquipe = new Dictionary<string, List<Guid>>();
        foreach (var equipe in new[] { "Águias", "Ursos" })
        {
            porEquipe[equipe] = [];
            for (var i = 0; i < 4; i++)
            {
                var nome = $"Atleta {equipe} {i}";
                await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, "/api/inscricoes", new
                {
                    temporadaId = c.TemporadaId, nome, cpf = CpfValido(++seed), nascimento = "1975-05-10", sexo = "Masculino", email = "atleta@exemplo.test",
                    telefone = "71999990000", alturaCm = 180, pesoKg = 80, posicao = "Ala", consentimentoLgpd = true, temporadaCategoriaIds = new[] { c.Tc },
                });
                var ficha = (await DadosDeTeste.Enviar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/inscricoes", null)).EnumerateArray()
                    .First(f => f.GetProperty("pessoa").GetProperty("nome").GetString() == nome);
                var pedido = ficha.GetProperty("categorias")[0].GetProperty("id").GetGuid();
                await DadosDeTeste.Enviar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/aprovar", new { });
                await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/inscricoes/{ficha.GetProperty("id").GetGuid()}/pagamentos/taxa", new { dataPagamento = "2026-10-09" });
                await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/equipes/{c.Equipes[equipe]}/atletas", new { inscricaoCategoriaId = pedido });
                var corpo = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/equipes")).Corpo).RootElement;
                porEquipe[equipe].Add((corpo.TryGetProperty("data", out var d) ? d : corpo).GetProperty("categorias")[0].GetProperty("equipes").EnumerateArray()
                    .First(e => e.GetProperty("nome").GetString() == equipe).GetProperty("atletas").EnumerateArray()
                    .First(a => a.GetProperty("nome").GetString() == nome).GetProperty("id").GetGuid());
            }
        }

        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", c.AssociacaoId));
        await DadosDeTeste.Enviar(arbitro, HttpMethod.Post, $"/api/jogos/{jogo.Id}/sumula");
        var lados = new[] { (Lado: 0, Time: jogo.Casa), (Lado: 1, Time: jogo.Visitante) };
        foreach (var (lado, time) in lados)
        {
            var ids = porEquipe[time];
            await DadosDeTeste.Enviar(arbitro, HttpMethod.Put, $"/api/jogos/{jogo.Id}/sumula/relacao",
                new { lado, capitaoAtletaId = ids[0], jogadores = ids.Select((id, i) => new { atletaId = id, numero = (i + 4).ToString(), titular = true }) });
        }
        (await Chamar(arbitro, HttpMethod.Post, $"/api/jogos/{jogo.Id}/sumula/iniciar")).Status.Should().Be(HttpStatusCode.NoContent);

        var r = await RegistrarWO(c, jogo.Id, casaAusente: true);

        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Corpo.Should().Contain("súmula já foi iniciada");
        (await Detalhe(c, jogo.Id)).GetProperty("status").GetString().Should().Be("EmAndamento");
    }

    // ------------------------------------------------------------------ desfazer

    [Fact]
    public async Task Desfazer_exige_motivo_volta_a_agendado_com_a_agenda_e_recalcula_a_classificacao()
    {
        if (SemBanco) return;
        var c = await NovoAsync("WoDesfazer", "Águias", "Ursos", "Lobos");
        var fase = await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        await GerarTabela(c);
        var jogo = (await Jogos(c))[0];
        var local = (await DadosDeTeste.Enviar(c.Admin, HttpMethod.Get, $"/api/associacoes/{c.AssociacaoId}/locais", null)).EnumerateArray().First().GetProperty("id").GetGuid();
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Patch, $"/api/jogos/{jogo.Id}", new { data = "2026-11-08", hora = "10:00:00", localId = local });
        (await TotalDePontos(c, fase)).Should().Be(0);
        await RegistrarWO(c, jogo.Id, casaAusente: false);
        (await TotalDePontos(c, fase)).Should().BeGreaterThan(0, "o W.O. dá pontos à equipe que vence");

        var semMotivo = await Desfazer(c, jogo.Id, "");
        semMotivo.Status.Should().Be(HttpStatusCode.BadRequest);
        semMotivo.Corpo.Should().Contain("motivo");
        (await Detalhe(c, jogo.Id)).GetProperty("status").GetString().Should().Be("WO", "sem motivo nada muda");

        (await Desfazer(c, jogo.Id, "marquei a equipe errada")).Status.Should().Be(HttpStatusCode.NoContent);

        var d = await Detalhe(c, jogo.Id);
        d.GetProperty("status").GetString().Should().Be("Agendado");
        d.GetProperty("placarCasa").ValueKind.Should().Be(JsonValueKind.Null);
        d.GetProperty("placarVisitante").ValueKind.Should().Be(JsonValueKind.Null);
        d.GetProperty("data").GetString().Should().Be("2026-11-08", "a agenda é mantida");
        d.GetProperty("hora").GetString().Should().Be("10:00:00");
        d.GetProperty("local").GetProperty("id").GetGuid().Should().Be(local);
        d.GetProperty("podeRegistrarWO").GetBoolean().Should().BeTrue();
        (await TotalDePontos(c, fase)).Should().Be(0, "a classificação é recalculada sem o W.O.");

        var historico = d.GetProperty("historico").EnumerateArray().ToList();
        historico.Select(h => h.GetProperty("acao").GetString()).Should().Equal("RegistrarWO", "DesfazerWO");
        historico[1].GetProperty("detalhe").GetString().Should().Contain("marquei a equipe errada");

        // Registrar de novo, agora com a outra equipe ausente, funciona.
        (await RegistrarWO(c, jogo.Id, casaAusente: true)).Status.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Desfazer_so_vale_para_jogo_em_wo()
    {
        if (SemBanco) return;
        var c = await NovoAsync("WoNaoWO", "Águias", "Ursos");
        await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        await GerarTabela(c);
        var jogo = (await Jogos(c))[0];

        var r = await Desfazer(c, jogo.Id, "sem W.O.");

        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Corpo.Should().Contain("não está em W.O.");
    }

    [Fact]
    public async Task Nao_desfaz_quando_a_fase_ja_definiu_as_equipes_da_fase_seguinte()
    {
        if (SemBanco) return;
        var c = await NovoAsync("WoDepende", "Águias", "Ursos", "Lobos");
        var corridos = await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        var final = await CriarFase(c, new
        {
            temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Final", tipo = "MataMata", jogosPorConfronto = 1, numeroConfrontos = 1, faseAnteriorId = corridos,
        });
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Put, $"/api/fases/{final}/confrontos/1", new
        {
            nome = "Final",
            origemA = new { tipo = "Colocacao", faseId = corridos, posicao = 1 },
            origemB = new { tipo = "Colocacao", faseId = corridos, posicao = 2 },
        });
        await GerarTabela(c);

        var tabela = (await Jogos(c)).Where(j => j.Fase == "Pontos").ToList();
        tabela.Should().HaveCount(3);
        foreach (var j in tabela) await RegistrarWO(c, j.Id, casaAusente: true);

        var finalDoJogo = (await Jogos(c)).First(j => j.Fase == "Final");
        finalDoJogo.Casa.Should().NotContain("º", "a fase terminou e as vagas da final já têm equipe");

        var r = await Desfazer(c, tabela[0].Id, "engano");

        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Corpo.Should().Contain("definiu as equipes da fase seguinte");
        var d = await Detalhe(c, tabela[0].Id);
        d.GetProperty("status").GetString().Should().Be("WO");
        d.GetProperty("podeDesfazerWO").GetBoolean().Should().BeFalse();
        d.GetProperty("motivoSemDesfazerWO").GetString().Should().Contain("definiu as equipes da fase seguinte");
    }

    [Fact]
    public async Task Desfazer_o_wo_que_decidiu_a_serie_reativa_o_jogo_se_necessario()
    {
        if (SemBanco) return;
        var c = await NovoAsync("WoSerie", "Águias", "Ursos");
        var fase = await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Série", tipo = "MataMata", jogosPorConfronto = 3, numeroConfrontos = 1 });
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Put, $"/api/fases/{fase}/confrontos/1", new
        {
            nome = "Decisão",
            origemA = new { tipo = "Equipe", equipeId = c.Equipes["Águias"] },
            origemB = new { tipo = "Equipe", equipeId = c.Equipes["Ursos"] },
        });
        await GerarTabela(c);
        var serie = await Jogos(c);
        serie.Should().HaveCount(3);

        // Os Ursos faltam nos dois primeiros jogos: as Águias vencem a série por 2 a 0 e o 3º jogo é dispensado.
        foreach (var j in serie.Take(2))
            await RegistrarWO(c, j.Id, casaAusente: j.Casa == "Ursos");
        (await Jogos(c))[2].Status.Should().Be("Dispensado");

        (await Desfazer(c, serie[1].Id, "os Ursos compareceram")).Status.Should().Be(HttpStatusCode.NoContent);

        var depois = await Jogos(c);
        depois[1].Status.Should().Be("Agendado");
        depois[2].Status.Should().Be("Agendado", "a série deixou de estar decidida: o jogo 'se necessário' voltou");
    }

    // ------------------------------------------------------------------ acesso

    [Fact]
    public async Task Arbitro_e_administrador_de_outra_associacao_nao_registram_nem_desfazem_wo()
    {
        if (SemBanco) return;
        var c = await NovoAsync("WoAcesso", "Águias", "Ursos");
        await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        await GerarTabela(c);
        var jogo = (await Jogos(c))[0];
        await RegistrarWO(c, jogo.Id, casaAusente: true);
        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", c.AssociacaoId));
        var alheio = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", Guid.NewGuid()));

        foreach (var cliente in new[] { arbitro, alheio })
        {
            (await Chamar(cliente, HttpMethod.Post, $"/api/jogos/{jogo.Id}/desfazer-wo", new { motivo = "invasão" })).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Chamar(cliente, HttpMethod.Post, $"/api/jogos/{jogo.Id}/wo", new { casaAusente = false })).Status.Should().Be(HttpStatusCode.Forbidden);
        }
        (await Detalhe(c, jogo.Id)).GetProperty("status").GetString().Should().Be("WO");
    }

    [Fact]
    public async Task Wo_de_jogo_com_equipes_indefinidas_e_recusado()
    {
        if (SemBanco) return;
        var c = await NovoAsync("WoIndefinido", "Águias", "Ursos", "Lobos");
        var corridos = await CriarFase(c, new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Pontos", tipo = "PontosCorridos", numeroTurnos = 1 });
        var final = await CriarFase(c, new
        {
            temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Final", tipo = "MataMata", jogosPorConfronto = 1, numeroConfrontos = 1, faseAnteriorId = corridos,
        });
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Put, $"/api/fases/{final}/confrontos/1", new
        {
            nome = "Final",
            origemA = new { tipo = "Colocacao", faseId = corridos, posicao = 1 },
            origemB = new { tipo = "Colocacao", faseId = corridos, posicao = 2 },
        });
        await GerarTabela(c);
        var jogoDaFinal = (await Jogos(c)).First(j => j.Fase == "Final");

        var r = await RegistrarWO(c, jogoDaFinal.Id, casaAusente: true);

        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Corpo.Should().Contain("equipes ainda não foram definidas");
    }
}
