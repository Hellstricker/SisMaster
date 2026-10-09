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
/// G9 pela API de verdade: ciclo das fases (criar, editar, mover, excluir), as recusas de estrutura e de sequência,
/// encerrar e reabrir o cadastro, e acesso por perfil. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class FasesG9ApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public FasesG9ApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private static async Task<(HttpStatusCode Status, string Corpo)> Chamar(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private sealed record Cenario(HttpClient Admin, Guid AssociacaoId, Guid CampeonatoId, Guid TemporadaId, Guid Tc);

    /// <summary>Associação, categoria, temporada com 4 equipes, ainda com as inscrições abertas (nada de fase).</summary>
    private async Task<Cenario> NovoAsync(string nome, bool encerrarInscricoes = true)
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

        if (encerrarInscricoes)
        {
            await send(HttpMethod.Patch, $"/api/campeonatos/{campeonato}/temporadas/{temporada}/encerrar-inscricoes", new { });
            foreach (var equipe in new[] { "Águias", "Ursos", "Lobos", "Tigres" })
                await send(HttpMethod.Post, $"/api/temporadas/{temporada}/equipes", new { temporadaId = temporada, temporadaCategoriaId = tc, nome = equipe, cor = "#ff0000" });
        }
        return new Cenario(admin, assoc, campeonato, temporada, tc);
    }

    private static object Corridos(Cenario c, string nome, Guid? anterior = null, int turnos = 1, int? classificados = null) => new
    {
        temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome, tipo = "PontosCorridos", numeroTurnos = turnos,
        classificadosPrimeiros = classificados, faseAnteriorId = anterior,
    };

    private static object MataMata(Cenario c, string nome, Guid? anterior, int melhorDe = 3, int confrontos = 2) => new
    {
        temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome, tipo = "MataMata", jogosPorConfronto = melhorDe, numeroConfrontos = confrontos, faseAnteriorId = anterior,
    };

    private static async Task<List<JsonElement>> Fases(Cenario c)
    {
        var corpo = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/fases")).Corpo).RootElement;
        var raiz = corpo.TryGetProperty("data", out var d) ? d : corpo;
        return raiz.GetProperty("categorias")[0].GetProperty("fases").EnumerateArray().Select(f => f.Clone()).ToList();
    }

    private static async Task<JsonElement> Estado(Cenario c)
    {
        var corpo = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/fases")).Corpo).RootElement;
        return (corpo.TryGetProperty("data", out var d) ? d : corpo).Clone();
    }

    private static async Task<Guid> CriarFase(Cenario c, object corpo)
    {
        var antes = (await Fases(c)).Select(f => f.GetProperty("id").GetGuid()).ToHashSet();
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases", corpo)).Status.Should().Be(HttpStatusCode.NoContent);
        return (await Fases(c)).Select(f => f.GetProperty("id").GetGuid()).First(id => !antes.Contains(id));
    }

    // ------------------------------------------------------------------ ciclo

    [Fact]
    public async Task So_cadastra_fase_depois_de_encerradas_as_inscricoes()
    {
        if (SemBanco) return;
        var c = await NovoAsync("FaseAbertas", encerrarInscricoes: false);

        var r = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases", Corridos(c, "Cedo demais"));

        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Corpo.Should().Contain("depois de encerradas as inscrições");
    }

    [Fact]
    public async Task Cria_em_sequencia_edita_e_exclui_renumerando_a_ordem()
    {
        if (SemBanco) return;
        var c = await NovoAsync("FaseCiclo");
        var primeira = await CriarFase(c, Corridos(c, "Classificatória"));
        var segunda = await CriarFase(c, MataMata(c, "Semifinais", primeira));
        var terceira = await CriarFase(c, MataMata(c, "Final", segunda, melhorDe: 5, confrontos: 1));

        (await Fases(c)).Select(f => f.GetProperty("ordem").GetInt32()).Should().Equal(1, 2, 3);

        var editar = await Chamar(c.Admin, HttpMethod.Patch, $"/api/fases/{primeira}",
            new { nome = "Fase de pontos", tipo = "PontosCorridos", numeroTurnos = 2, classificadosPrimeiros = 4, faseAnteriorId = (Guid?)null });
        editar.Status.Should().Be(HttpStatusCode.NoContent);
        var depois = (await Fases(c)).First(f => f.GetProperty("id").GetGuid() == primeira);
        depois.GetProperty("nome").GetString().Should().Be("Fase de pontos");
        depois.GetProperty("numeroTurnos").GetInt32().Should().Be(2);

        (await Chamar(c.Admin, HttpMethod.Delete, $"/api/fases/{terceira}")).Status.Should().Be(HttpStatusCode.NoContent);
        var restantes = await Fases(c);
        restantes.Should().HaveCount(2);
        restantes.Select(f => f.GetProperty("ordem").GetInt32()).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Recusa_estrutura_invalida_por_tipo()
    {
        if (SemBanco) return;
        var c = await NovoAsync("FaseEstrutura");

        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases", Corridos(c, "Turnos demais", turnos: 5)))
            .Corpo.Should().Contain("turnos deve estar entre 1 e 3");
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases", MataMata(c, "Melhor de 4", null, melhorDe: 4)))
            .Corpo.Should().Contain("melhor de 1, 3, 5 ou 7");
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases", MataMata(c, "Confrontos demais", null, confrontos: 17)))
            .Corpo.Should().Contain("entre 1 e 16");
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases",
            new { temporadaId = c.TemporadaId, temporadaCategoriaId = c.Tc, nome = "Grupos demais", tipo = "Grupos", numeroTurnos = 1, numeroGrupos = 3, distribuicao = "Serpentina" }))
            .Corpo.Should().Contain("não formam 3 grupos");
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases", Corridos(c, "Classifica demais", classificados: 9)))
            .Corpo.Should().Contain("entre 1 e 4");

        (await Fases(c)).Should().BeEmpty("nenhuma fase inválida foi gravada");
    }

    // ------------------------------------------------------------------ sequência

    [Fact]
    public async Task Fase_que_tem_outra_depois_dela_nao_e_excluida_e_a_ordem_respeita_a_origem()
    {
        if (SemBanco) return;
        var c = await NovoAsync("FaseSequencia");
        var primeira = await CriarFase(c, Corridos(c, "Primeira"));
        var segunda = await CriarFase(c, MataMata(c, "Segunda", primeira));

        var excluir = await Chamar(c.Admin, HttpMethod.Delete, $"/api/fases/{primeira}");
        excluir.Status.Should().Be(HttpStatusCode.BadRequest);
        excluir.Corpo.Should().Contain("outra fase vem desta");

        var descer = await Chamar(c.Admin, HttpMethod.Patch, $"/api/fases/{primeira}/mover", new { direcao = 1 });
        descer.Status.Should().Be(HttpStatusCode.BadRequest);
        descer.Corpo.Should().Contain("precisa ficar antes");

        var subir = await Chamar(c.Admin, HttpMethod.Patch, $"/api/fases/{primeira}/mover", new { direcao = -1 });
        subir.Status.Should().Be(HttpStatusCode.BadRequest);
        subir.Corpo.Should().Contain("já é a primeira");

        var daOutraCategoria = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases", Corridos(c, "Volta", anterior: Guid.NewGuid()));
        daOutraCategoria.Status.Should().Be(HttpStatusCode.BadRequest);

        (await Fases(c)).Select(f => f.GetProperty("id").GetGuid()).Should().Equal(primeira, segunda);
    }

    [Fact]
    public async Task Fases_independentes_trocam_de_lugar()
    {
        if (SemBanco) return;
        var c = await NovoAsync("FaseMover");
        var a = await CriarFase(c, Corridos(c, "A"));
        var b = await CriarFase(c, Corridos(c, "B"));

        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/fases/{b}/mover", new { direcao = -1 })).Status.Should().Be(HttpStatusCode.NoContent);

        (await Fases(c)).Select(f => f.GetProperty("id").GetGuid()).Should().Equal(b, a);
    }

    // ------------------------------------------------------------------ encerrar e reabrir o cadastro

    [Fact]
    public async Task Encerrar_exige_fase_cadastrada_e_trava_estrutura_mas_nao_o_nome()
    {
        if (SemBanco) return;
        var c = await NovoAsync("FaseEncerrar");

        var vazio = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/encerrar-cadastro", new { });
        vazio.Status.Should().Be(HttpStatusCode.BadRequest);
        vazio.Corpo.Should().Contain("ao menos uma fase");

        var fase = await CriarFase(c, Corridos(c, "Única"));
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/encerrar-cadastro", new { })).Status.Should().Be(HttpStatusCode.NoContent);
        (await Estado(c)).GetProperty("cadastroEncerrado").GetBoolean().Should().BeTrue();

        var nova = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases", Corridos(c, "Tarde demais"));
        nova.Status.Should().Be(HttpStatusCode.BadRequest);
        nova.Corpo.Should().Contain("cadastro de fases está encerrado");

        var estrutura = await Chamar(c.Admin, HttpMethod.Patch, $"/api/fases/{fase}", new { nome = "Única", tipo = "PontosCorridos", numeroTurnos = 2 });
        estrutura.Status.Should().Be(HttpStatusCode.BadRequest);
        estrutura.Corpo.Should().Contain("só o nome");

        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/fases/{fase}", new { nome = "Fase principal", tipo = "PontosCorridos", numeroTurnos = 1 }))
            .Status.Should().Be(HttpStatusCode.NoContent);
        (await Fases(c))[0].GetProperty("nome").GetString().Should().Be("Fase principal");

        (await Chamar(c.Admin, HttpMethod.Delete, $"/api/fases/{fase}")).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/fases/{fase}/mover", new { direcao = 1 })).Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reabre_o_cadastro_ate_a_tabela_de_jogos_ser_gerada()
    {
        if (SemBanco) return;
        var c = await NovoAsync("FaseReabrir");
        var fase = await CriarFase(c, Corridos(c, "Única"));

        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/reabrir-cadastro", new { })).Corpo.Should().Contain("não está encerrado");

        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/encerrar-cadastro", new { });
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/reabrir-cadastro", new { })).Status.Should().Be(HttpStatusCode.NoContent);
        (await Estado(c)).GetProperty("cadastroEncerrado").GetBoolean().Should().BeFalse();
        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/fases/{fase}", new { nome = "Única", tipo = "PontosCorridos", numeroTurnos = 2 })).Status.Should().Be(HttpStatusCode.NoContent);

        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/encerrar-cadastro", new { });
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/tabela-jogos", new { });

        var reabrir = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/reabrir-cadastro", new { });
        reabrir.Status.Should().Be(HttpStatusCode.BadRequest);
        reabrir.Corpo.Should().Contain("tabela de jogos já foi gerada");
        (await Estado(c)).GetProperty("podeReabrir").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Fase_com_estrutura_incompleta_impede_encerrar_o_cadastro()
    {
        if (SemBanco) return;
        var c = await NovoAsync("FaseIncompleta");
        var fase = await CriarFase(c, Corridos(c, "Antiga"));

        // Simula uma fase cadastrada antes de existirem os campos de estrutura.
        using (var escopo = _fabrica.Services.CreateScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<CampeonatoDbContext>();
            var f = await db.Fases.FirstAsync(x => x.Id == fase);
            db.Entry(f).Property(x => x.NumeroTurnos).CurrentValue = null;
            await db.SaveChangesAsync();
        }

        (await Fases(c))[0].GetProperty("estruturaCompleta").GetBoolean().Should().BeFalse();
        var encerrar = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/encerrar-cadastro", new { });
        encerrar.Status.Should().Be(HttpStatusCode.BadRequest);
        encerrar.Corpo.Should().Contain("Complete a estrutura").And.Contain("Antiga");

        (await Chamar(c.Admin, HttpMethod.Patch, $"/api/fases/{fase}", new { nome = "Antiga", tipo = "PontosCorridos", numeroTurnos = 1 })).Status.Should().Be(HttpStatusCode.NoContent);
        (await Fases(c))[0].GetProperty("estruturaCompleta").GetBoolean().Should().BeTrue();
        (await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/encerrar-cadastro", new { })).Status.Should().Be(HttpStatusCode.NoContent);
    }

    // ------------------------------------------------------------------ acesso

    [Fact]
    public async Task Arbitro_nao_gerencia_fases()
    {
        if (SemBanco) return;
        var c = await NovoAsync("FaseArbitro");
        var fase = await CriarFase(c, Corridos(c, "Da diretoria"));
        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", c.AssociacaoId));

        (await Chamar(arbitro, HttpMethod.Get, $"/api/temporadas/{c.TemporadaId}/fases")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(arbitro, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases", Corridos(c, "Intrusa"))).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(arbitro, HttpMethod.Patch, $"/api/fases/{fase}", new { nome = "X", tipo = "PontosCorridos", numeroTurnos = 1 })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(arbitro, HttpMethod.Delete, $"/api/fases/{fase}")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(arbitro, HttpMethod.Post, $"/api/temporadas/{c.TemporadaId}/fases/encerrar-cadastro", new { })).Status.Should().Be(HttpStatusCode.Forbidden);

        (await Fases(c)).Should().ContainSingle().Which.GetProperty("nome").GetString().Should().Be("Da diretoria");
    }

    [Fact]
    public async Task Administrador_de_outra_associacao_nao_mexe_nas_fases()
    {
        if (SemBanco) return;
        var c = await NovoAsync("FaseAlheia");
        var fase = await CriarFase(c, Corridos(c, "Protegida"));
        var alheio = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", Guid.NewGuid()));

        (await Chamar(alheio, HttpMethod.Patch, $"/api/fases/{fase}", new { nome = "Invadida", tipo = "PontosCorridos", numeroTurnos = 1 })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(alheio, HttpMethod.Delete, $"/api/fases/{fase}")).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(alheio, HttpMethod.Patch, $"/api/fases/{fase}/mover", new { direcao = 1 })).Status.Should().Be(HttpStatusCode.Forbidden);
        (await Chamar(alheio, HttpMethod.Get, $"/api/fases/{fase}")).Status.Should().Be(HttpStatusCode.Forbidden);

        (await Fases(c)).Should().ContainSingle().Which.GetProperty("nome").GetString().Should().Be("Protegida");
    }
}
