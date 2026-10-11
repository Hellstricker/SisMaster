using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// G13 pela API de verdade: preparar a súmula, a relação de cada equipe (elenco, camisa, titulares, capitão), as recusas,
/// iniciar, a relação travada e o atleta acrescentado depois do início. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class SumulaG13ApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private static int _semente = 95000;
    private readonly ApiFactory _fabrica;

    public SumulaG13ApiTests(ApiFactory fabrica) => _fabrica = fabrica;

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

    private sealed record Time(int Lado, string Nome, List<Guid> Atletas);
    private sealed record Cenario(HttpClient Admin, HttpClient Arbitro, Guid AssociacaoId, Guid JogoId, Guid SumulaId, Time Casa, Time Visitante);

    /// <summary>Águias (13 atletas) × Ursos (5 atletas), tabela gerada e súmula do jogo 1 preparada.</summary>
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
        await Chamar(admin, HttpMethod.Patch, $"/api/temporadas/{temporada}/taxa-inscricao", new { taxa = 100 });
        await send(HttpMethod.Patch, $"/api/campeonatos/{campeonato}/temporadas/{temporada}/encerrar-inscricoes", new { });

        var equipes = new Dictionary<string, Guid>();
        foreach (var e in new[] { "Águias", "Ursos" })
            await send(HttpMethod.Post, $"/api/temporadas/{temporada}/equipes", new { temporadaId = temporada, temporadaCategoriaId = tc, nome = e, cor = "#ff0000" });
        var json = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{temporada}/equipes")).Corpo).RootElement;
        foreach (var e in (json.TryGetProperty("data", out var d) ? d : json).GetProperty("categorias")[0].GetProperty("equipes").EnumerateArray())
            equipes[e.GetProperty("nome").GetString()!] = e.GetProperty("id").GetGuid();

        foreach (var (equipe, quantos) in new[] { ("Águias", 13), ("Ursos", 5) })
            for (var i = 0; i < quantos; i++)
            {
                var atleta = $"{equipe} atleta {i:00}";
                await send(HttpMethod.Post, "/api/inscricoes", new
                {
                    temporadaId = temporada, nome = atleta, cpf = CpfValido(Interlocked.Increment(ref _semente)), nascimento = "1975-05-10", sexo = "Masculino",
                    email = "atleta@exemplo.test", telefone = "71999990000", alturaCm = 180, pesoKg = 80, posicao = "Ala", consentimentoLgpd = true, temporadaCategoriaIds = new[] { tc },
                });
                var ficha = (await send(HttpMethod.Get, $"/api/temporadas/{temporada}/inscricoes", null)).EnumerateArray()
                    .First(f => f.GetProperty("pessoa").GetProperty("nome").GetString() == atleta);
                var pedido = ficha.GetProperty("categorias")[0].GetProperty("id").GetGuid();
                await send(HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/aprovar", new { });
                await send(HttpMethod.Post, $"/api/inscricoes/{ficha.GetProperty("id").GetGuid()}/pagamentos/taxa", new { dataPagamento = "2026-10-09" });
                await send(HttpMethod.Post, $"/api/equipes/{equipes[equipe]}/atletas", new { inscricaoCategoriaId = pedido });
            }

        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/fases", new { temporadaId = temporada, temporadaCategoriaId = tc, nome = "Fase única", tipo = "PontosCorridos", numeroTurnos = 1 });
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/fases/encerrar-cadastro", new { });
        await send(HttpMethod.Post, $"/api/temporadas/{temporada}/tabela-jogos", new { });
        var jogo = (await send(HttpMethod.Get, $"/api/temporadas/{temporada}/jogos", null)).GetProperty("jogos").EnumerateArray().First().GetProperty("id").GetGuid();

        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", assoc));
        await send(HttpMethod.Post, $"/api/jogos/{jogo}/sumula", null);
        var preparo = await Preparo(arbitro, jogo);
        var times = preparo.GetProperty("times").EnumerateArray().Select(t => new Time(
            t.GetProperty("lado").GetString() == "Casa" ? 0 : 1, t.GetProperty("nome").GetString()!,
            t.GetProperty("elenco").EnumerateArray().OrderBy(a => a.GetProperty("nome").GetString()).Select(a => a.GetProperty("atletaId").GetGuid()).ToList())).ToList();
        return new Cenario(admin, arbitro, assoc, jogo, preparo.GetProperty("sumula").GetProperty("id").GetGuid(), times.First(t => t.Lado == 0), times.First(t => t.Lado == 1));
    }

    private static async Task<JsonElement> Preparo(HttpClient c, Guid jogo)
    {
        var corpo = JsonDocument.Parse((await Chamar(c, HttpMethod.Get, $"/api/jogos/{jogo}/sumula")).Corpo).RootElement;
        return corpo.Clone();
    }

    private static object Relacao(Time t, int quantos, int titulares = 5, int? capitao = 0, string? tecnico = null, Func<int, string>? camisa = null) => new
    {
        lado = t.Lado,
        tecnico,
        capitaoAtletaId = capitao is { } i ? t.Atletas[i] : (Guid?)null,
        jogadores = t.Atletas.Take(quantos).Select((id, k) => new { atletaId = id, numero = (camisa?.Invoke(k) ?? (k + 4).ToString()), titular = k < titulares }).ToList(),
    };

    private static Task<(HttpStatusCode Status, string Corpo)> Salvar(Cenario c, object relacao) =>
        Chamar(c.Arbitro, HttpMethod.Put, $"/api/jogos/{c.JogoId}/sumula/relacao", relacao);

    // ------------------------------------------------------------------ preparar e relacionar

    [Fact]
    public async Task O_preparo_traz_as_regras_e_o_elenco_de_cada_equipe_e_nao_deixa_preparar_duas_vezes()
    {
        if (SemBanco) return;
        var c = await NovoAsync("SumPreparo");

        var preparo = await Preparo(c.Arbitro, c.JogoId);
        preparo.GetProperty("existe").GetBoolean().Should().BeTrue();
        var regras = preparo.GetProperty("regras");
        regras.GetProperty("maximo").GetInt32().Should().Be(12);
        regras.GetProperty("minimoParaIniciar").GetInt32().Should().Be(4);
        regras.GetProperty("titulares").GetInt32().Should().Be(5);
        regras.GetProperty("minimoParaRodizio").GetInt32().Should().Be(7);
        preparo.GetProperty("categoriaComRodizio").GetBoolean().Should().BeTrue();
        new[] { c.Casa, c.Visitante }.Select(t => t.Atletas.Count).OrderBy(x => x).Should().Equal(5, 13);

        var duas = await Chamar(c.Arbitro, HttpMethod.Post, $"/api/jogos/{c.JogoId}/sumula");
        duas.Status.Should().Be(HttpStatusCode.BadRequest);
        duas.Corpo.Should().Contain("já tem súmula");
    }

    [Fact]
    public async Task Relacao_valida_e_salva_com_capitao_titulares_e_comissao_e_substitui_a_anterior()
    {
        if (SemBanco) return;
        var c = await NovoAsync("SumRelacao");

        (await Salvar(c, Relacao(c.Casa, 6, tecnico: "Prof. Carlos"))).Status.Should().Be(HttpStatusCode.NoContent);
        var time = (await Preparo(c.Arbitro, c.JogoId)).GetProperty("times").EnumerateArray().First(t => t.GetProperty("lado").GetString() == "Casa");
        time.GetProperty("jogadores").GetArrayLength().Should().Be(6);
        time.GetProperty("tecnico").GetString().Should().Be("Prof. Carlos");
        time.GetProperty("capitaoAtletaId").GetGuid().Should().Be(c.Casa.Atletas[0]);
        time.GetProperty("jogadores").EnumerateArray().Count(j => j.GetProperty("titular").GetBoolean()).Should().Be(5);

        // Salvar de novo substitui tudo (menos jogadores, outro capitão).
        (await Salvar(c, Relacao(c.Casa, 5, capitao: 2))).Status.Should().Be(HttpStatusCode.NoContent);
        var depois = (await Preparo(c.Arbitro, c.JogoId)).GetProperty("times").EnumerateArray().First(t => t.GetProperty("lado").GetString() == "Casa");
        depois.GetProperty("jogadores").GetArrayLength().Should().Be(5);
        depois.GetProperty("capitaoAtletaId").GetGuid().Should().Be(c.Casa.Atletas[2]);
    }

    [Fact]
    public async Task Recusa_camisa_repetida_invalida_mais_de_12_mais_de_5_titulares_e_capitao_fora_da_relacao()
    {
        if (SemBanco) return;
        var c = await NovoAsync("SumRecusas");
        // A casa tem 13 ou 5 atletas conforme o sorteio da tabela; usa a que tem 13 para o limite.
        var grande = c.Casa.Atletas.Count == 13 ? c.Casa : c.Visitante;

        var repetida = await Salvar(c, Relacao(grande, 6, camisa: _ => "7"));
        repetida.Status.Should().Be(HttpStatusCode.BadRequest);
        repetida.Corpo.Should().Contain("camisa 7").And.Contain("repetida");

        var invalida = await Salvar(c, Relacao(grande, 5, camisa: k => k == 0 ? "100" : (k + 10).ToString()));
        invalida.Status.Should().Be(HttpStatusCode.BadRequest);
        invalida.Corpo.Should().Contain("Camisa inválida");

        var demais = await Salvar(c, Relacao(grande, 13, titulares: 5));
        demais.Status.Should().Be(HttpStatusCode.BadRequest);
        demais.Corpo.Should().Contain("No máximo 12");

        var titulares = await Salvar(c, Relacao(grande, 8, titulares: 6));
        titulares.Status.Should().Be(HttpStatusCode.BadRequest);
        titulares.Corpo.Should().Contain("No máximo 5 titulares");

        var capitaoFora = await Salvar(c, Relacao(grande, 5, capitao: 9));
        capitaoFora.Status.Should().Be(HttpStatusCode.BadRequest);
        capitaoFora.Corpo.Should().Contain("capitão precisa estar entre os relacionados");

        var relacionados = (await Preparo(c.Arbitro, c.JogoId)).GetProperty("times").EnumerateArray()
            .First(t => t.GetProperty("nome").GetString() == grande.Nome).GetProperty("jogadores").GetArrayLength();
        relacionados.Should().Be(0, "nenhuma relação inválida foi gravada");
    }

    [Fact]
    public async Task So_relaciona_atleta_do_elenco_da_propria_equipe()
    {
        if (SemBanco) return;
        var c = await NovoAsync("SumElenco");
        var intruso = new
        {
            lado = c.Casa.Lado, capitaoAtletaId = c.Visitante.Atletas[0],
            jogadores = new[] { new { atletaId = c.Visitante.Atletas[0], numero = "4", titular = true } },
        };

        var r = await Salvar(c, intruso);

        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Corpo.Should().Contain("não pertence ao elenco");
    }

    // ------------------------------------------------------------------ iniciar

    [Fact]
    public async Task Iniciar_lista_o_que_falta_e_com_tudo_pronto_trava_a_relacao()
    {
        if (SemBanco) return;
        var c = await NovoAsync("SumIniciar");
        var iniciar = () => Chamar(c.Arbitro, HttpMethod.Post, $"/api/jogos/{c.JogoId}/sumula/iniciar");

        var vazia = await iniciar();
        vazia.Status.Should().Be(HttpStatusCode.BadRequest);
        vazia.Corpo.Should().Contain("mínimo de 4 relacionados").And.Contain("definir o capitão");

        await Salvar(c, Relacao(c.Casa, 5));
        var umLado = await iniciar();
        umLado.Status.Should().Be(HttpStatusCode.BadRequest);
        umLado.Corpo.Should().Contain(c.Visitante.Nome, "a outra equipe ainda não tem relação");

        await Salvar(c, Relacao(c.Visitante, 4, titulares: 4)); // com 4 relacionados todos começam
        (await iniciar()).Status.Should().Be(HttpStatusCode.NoContent);
        (await Preparo(c.Arbitro, c.JogoId)).GetProperty("sumula").GetProperty("status").GetString().Should().Be("EmAndamento");

        var trava = await Salvar(c, Relacao(c.Casa, 6));
        trava.Status.Should().Be(HttpStatusCode.BadRequest, "a relação trava quando a súmula é iniciada");
        (await iniciar()).Status.Should().Be(HttpStatusCode.BadRequest, "não se inicia duas vezes");
    }

    [Fact]
    public async Task Atleta_chegou_depois_e_acrescentado_ao_banco_respeitando_camisa_e_elenco()
    {
        if (SemBanco) return;
        var c = await NovoAsync("SumAcrescenta");
        await Salvar(c, Relacao(c.Casa, 5));
        await Salvar(c, Relacao(c.Visitante, 4, titulares: 4));
        await DadosDeTeste.Enviar(c.Arbitro, HttpMethod.Post, $"/api/jogos/{c.JogoId}/sumula/iniciar");
        var acrescentar = (Time t, int atleta, string camisa) => Chamar(c.Arbitro, HttpMethod.Post, $"/api/sumulas/{c.SumulaId}/relacionados",
            new { sumulaId = c.SumulaId, lado = t.Lado, atletaId = t.Atletas[atleta], numero = camisa });

        (await acrescentar(c.Visitante, 4, "4")).Status.Should().Be(HttpStatusCode.BadRequest, "a camisa 4 já é de outro atleta da equipe");
        (await acrescentar(c.Visitante, 0, "20")).Status.Should().Be(HttpStatusCode.BadRequest, "o atleta já está relacionado");

        (await acrescentar(c.Visitante, 4, "20")).Status.Should().Be(HttpStatusCode.NoContent);
        var visitante = (await Preparo(c.Arbitro, c.JogoId)).GetProperty("times").EnumerateArray().First(t => t.GetProperty("lado").GetString() == "Visitante");
        visitante.GetProperty("jogadores").GetArrayLength().Should().Be(5);
        var novo = visitante.GetProperty("jogadores").EnumerateArray().First(j => j.GetProperty("atletaId").GetGuid() == c.Visitante.Atletas[4]);
        novo.GetProperty("titular").GetBoolean().Should().BeFalse("quem chega depois entra no banco");
        novo.GetProperty("chegouNoPeriodo").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    // ------------------------------------------------------------------ acesso

    [Fact]
    public async Task Gestao_e_arbitro_da_associacao_operam_e_os_de_fora_nao()
    {
        if (SemBanco) return;
        var c = await NovoAsync("SumAcesso");
        var arbitroDeFora = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", Guid.NewGuid()));
        var adminDeFora = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", Guid.NewGuid()));
        var gestor = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", c.AssociacaoId));

        foreach (var de_fora in new[] { arbitroDeFora, adminDeFora })
        {
            (await Chamar(de_fora, HttpMethod.Get, $"/api/jogos/{c.JogoId}/sumula")).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Chamar(de_fora, HttpMethod.Put, $"/api/jogos/{c.JogoId}/sumula/relacao", Relacao(c.Casa, 5))).Status.Should().Be(HttpStatusCode.Forbidden);
            (await Chamar(de_fora, HttpMethod.Post, $"/api/jogos/{c.JogoId}/sumula/iniciar")).Status.Should().Be(HttpStatusCode.Forbidden);
        }

        // O administrador da própria associação também prepara a súmula (modo gestão), como o árbitro (modo árbitro).
        (await Chamar(gestor, HttpMethod.Put, $"/api/jogos/{c.JogoId}/sumula/relacao", Relacao(c.Casa, 5))).Status.Should().Be(HttpStatusCode.NoContent);
        (await Preparo(c.Arbitro, c.JogoId)).GetProperty("times").EnumerateArray().First(t => t.GetProperty("lado").GetString() == "Casa")
            .GetProperty("jogadores").GetArrayLength().Should().Be(5);
    }

    [Fact]
    public async Task Atleta_acrescentado_por_quem_e_de_outra_associacao_e_recusado()
    {
        if (SemBanco) return;
        var c = await NovoAsync("SumAcrescAcesso");
        await Salvar(c, Relacao(c.Casa, 5));
        await Salvar(c, Relacao(c.Visitante, 4, titulares: 4));
        await DadosDeTeste.Enviar(c.Arbitro, HttpMethod.Post, $"/api/jogos/{c.JogoId}/sumula/iniciar");
        var de_fora = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", Guid.NewGuid()));

        var r = await Chamar(de_fora, HttpMethod.Post, $"/api/sumulas/{c.SumulaId}/relacionados",
            new { sumulaId = c.SumulaId, lado = c.Visitante.Lado, atletaId = c.Visitante.Atletas[4], numero = "20" });

        r.Status.Should().Be(HttpStatusCode.Forbidden);
        (await Preparo(c.Arbitro, c.JogoId)).GetProperty("times").EnumerateArray().First(t => t.GetProperty("lado").GetString() == "Visitante")
            .GetProperty("jogadores").GetArrayLength().Should().Be(4);
    }

    // ------------------------------------------------------------------ A4: eventos do mesário de estatísticas

    private async Task<(Cenario C, Guid Jogador)> IniciadaComJogadorAsync(string nome)
    {
        var c = await NovoAsync(nome);
        await Salvar(c, Relacao(c.Casa, 6));
        await Salvar(c, Relacao(c.Visitante, 5));
        (await Chamar(c.Arbitro, HttpMethod.Post, $"/api/jogos/{c.JogoId}/sumula/iniciar")).Status.Should().Be(HttpStatusCode.NoContent);
        var casa = (await Preparo(c.Arbitro, c.JogoId)).GetProperty("times").EnumerateArray().First(t => t.GetProperty("lado").GetString() == "Casa");
        return (c, casa.GetProperty("jogadores").EnumerateArray().First().GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Evento_reenviado_com_o_mesmo_id_nao_duplica_e_grava_tempo_e_periodo()
    {
        if (SemBanco) return;
        var (c, jogador) = await IniciadaComJogadorAsync("A4Idem");
        var id = Guid.NewGuid();
        var corpo = new { eventoId = id, jogadorId = jogador, tipo = "Ponto2", periodo = 1, tempoJogoSegundos = 421 };

        (await Chamar(c.Arbitro, HttpMethod.Post, $"/api/sumulas/{c.SumulaId}/eventos", corpo)).Status.Should().Be(HttpStatusCode.NoContent);
        (await Chamar(c.Arbitro, HttpMethod.Post, $"/api/sumulas/{c.SumulaId}/eventos", corpo)).Status.Should().Be(HttpStatusCode.NoContent, "reenviar é inofensivo");

        var sumula = JsonDocument.Parse((await Chamar(c.Arbitro, HttpMethod.Get, $"/api/sumulas/{c.SumulaId}/sumula")).Corpo).RootElement;
        var dados = sumula.TryGetProperty("data", out var d) ? d : sumula;
        var eventos = dados.GetProperty("eventos").EnumerateArray().ToList();
        eventos.Should().HaveCount(1);
        eventos[0].GetProperty("tempoJogoSegundos").GetInt32().Should().Be(421);
        dados.GetProperty("placarCasa").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Com_5_faltas_o_servidor_recusa_novos_lancamentos_e_o_rodizio_mostra_as_faltas()
    {
        if (SemBanco) return;
        var (c, jogador) = await IniciadaComJogadorAsync("A4Cinco");
        var lancar = (string tipo) => Chamar(c.Arbitro, HttpMethod.Post, $"/api/sumulas/{c.SumulaId}/eventos",
            new { eventoId = Guid.NewGuid(), jogadorId = jogador, tipo, periodo = 1, tempoJogoSegundos = 300 });

        for (var i = 0; i < 5; i++) (await lancar("FaltaPessoal")).Status.Should().Be(HttpStatusCode.NoContent);
        var sexta = await lancar("Ponto2");

        sexta.Status.Should().Be(HttpStatusCode.BadRequest);
        sexta.Corpo.Should().Contain("5 faltas");
        var rodizio = JsonDocument.Parse((await Chamar(c.Arbitro, HttpMethod.Get, $"/api/sumulas/{c.SumulaId}/rodizio")).Corpo).RootElement;
        var dados = rodizio.TryGetProperty("data", out var d) ? d : rodizio;
        dados.GetProperty("casa").GetProperty("jogadores").EnumerateArray()
            .First(j => j.GetProperty("id").GetGuid() == jogador).GetProperty("faltas").GetInt32().Should().Be(5);
    }
}
