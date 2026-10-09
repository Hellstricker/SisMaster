using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// G6 pela API de verdade: resumo da temporada, regras de iniciar/encerrar, cobranças, inscrição pela diretoria com as
/// inscrições encerradas e datas opcionais da competição. Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class TemporadaG6ApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public TemporadaG6ApiTests(ApiFactory fabrica) => _fabrica = fabrica;

    private HttpClient Admin() => _fabrica.ClienteCom(ApiFactory.Token("Administrador"));

    private static async Task<(HttpStatusCode Status, string Corpo)> Chamar(HttpClient c, HttpMethod m, string url, object? corpo = null)
    {
        var req = new HttpRequestMessage(m, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        var resp = await c.SendAsync(req);
        return (resp.StatusCode, await resp.Content.ReadAsStringAsync());
    }

    private static object Ficha(Guid temporada, Guid tc, string cpf = "39053344705", string nome = "Carlos Souza Lima") => new
    {
        temporadaId = temporada, nome, cpf, nascimento = "1980-05-10", sexo = "Masculino", email = "carlos@exemplo.test",
        telefone = "71999990000", consentimentoLgpd = true, temporadaCategoriaIds = new[] { tc },
    };

    private static async Task<(Guid Tc, Guid Categoria)> PrimeiraCategoria(HttpClient admin, Guid temporada)
    {
        var (_, corpo) = await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{temporada}");
        var cat = JsonDocument.Parse(corpo).RootElement.GetProperty("categorias").EnumerateArray().First();
        return (cat.GetProperty("id").GetGuid(), cat.GetProperty("categoriaId").GetGuid());
    }

    [Fact]
    public async Task Resumo_conta_fichas_pendentes_jogos_e_uso_da_categoria()
    {
        if (SemBanco) return;
        var admin = Admin();
        var e = await DadosDeTeste.CriarAsync(admin, "ResumoG6");
        var (tc, _) = await PrimeiraCategoria(admin, e.TemporadaId);

        (await Chamar(admin, HttpMethod.Post, "/api/inscricoes", Ficha(e.TemporadaId, tc))).Status.Should().Be(HttpStatusCode.NoContent);
        var (status, corpo) = await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{e.TemporadaId}/resumo");

        status.Should().Be(HttpStatusCode.OK);
        var r = JsonDocument.Parse(corpo).RootElement;
        r.GetProperty("fichas").GetInt32().Should().Be(1);
        r.GetProperty("pedidosPendentes").GetInt32().Should().Be(1);
        r.GetProperty("equipes").GetInt32().Should().Be(2);
        r.GetProperty("fases").GetInt32().Should().Be(1);
        r.GetProperty("jogosTotal").GetInt32().Should().Be(1);
        r.GetProperty("jogosEmAberto").GetInt32().Should().Be(1);
        r.GetProperty("categorias").GetProperty(tc.ToString()).GetProperty("pedidos").GetInt32().Should().Be(1);
        r.GetProperty("cobranca").GetProperty("geradaEm").ValueKind.Should().Be(JsonValueKind.Null);
        r.GetProperty("cobranca").GetProperty("fichasSemCobranca").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task A_diretoria_inscreve_com_as_inscricoes_encerradas()
    {
        if (SemBanco) return;
        var admin = Admin();
        var e = await DadosDeTeste.CriarAsync(admin, "DiretoriaG6"); // a estrutura já encerra as inscrições
        var (tc, _) = await PrimeiraCategoria(admin, e.TemporadaId);

        var (status, corpo) = await Chamar(admin, HttpMethod.Post, "/api/inscricoes", Ficha(e.TemporadaId, tc));

        status.Should().Be(HttpStatusCode.NoContent, corpo);
    }

    [Fact]
    public async Task Cobrancas_exigem_valor_em_todas_as_categorias_e_depois_travam_a_reabertura()
    {
        if (SemBanco) return;
        var admin = Admin();
        var e = await DadosDeTeste.CriarAsync(admin, "CobrancaG6");
        var (tc, categoria) = await PrimeiraCategoria(admin, e.TemporadaId);
        await Chamar(admin, HttpMethod.Post, "/api/inscricoes", Ficha(e.TemporadaId, tc));

        var semValor = await Chamar(admin, HttpMethod.Post, $"/api/temporadas/{e.TemporadaId}/gerar-cobrancas");
        semValor.Status.Should().Be(HttpStatusCode.BadRequest);
        semValor.Corpo.Should().Contain("valor de todas as categorias");

        (await Chamar(admin, HttpMethod.Patch, $"/api/temporadas/{e.TemporadaId}/categorias/{categoria}/valor", new { valor = 500 })).Status.Should().Be(HttpStatusCode.NoContent);
        (await Chamar(admin, HttpMethod.Post, $"/api/temporadas/{e.TemporadaId}/gerar-cobrancas")).Status.Should().Be(HttpStatusCode.NoContent);

        var reabrir = await Chamar(admin, HttpMethod.Patch,
            $"/api/campeonatos/{e.CampeonatoId}/temporadas/{e.TemporadaId}/inscricoes-habilitadas", new { habilitadas = true });
        reabrir.Status.Should().Be(HttpStatusCode.BadRequest);
        reabrir.Corpo.Should().Contain("cobranças geradas");

        // A diretoria ainda inscreve depois das cobranças; o resumo avisa que há ficha sem cobrança.
        (await Chamar(admin, HttpMethod.Post, "/api/inscricoes", Ficha(e.TemporadaId, tc, "11144477735", "Ana Paula Reis"))).Status.Should().Be(HttpStatusCode.NoContent);
        var resumo = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{e.TemporadaId}/resumo")).Corpo).RootElement.GetProperty("cobranca");
        resumo.GetProperty("fichasComCobranca").GetInt32().Should().Be(1);
        resumo.GetProperty("fichasSemCobranca").GetInt32().Should().Be(1);
        resumo.GetProperty("previsto").GetDecimal().Should().Be(500m);
    }

    [Fact]
    public async Task Reabrir_a_inscricao_funciona_enquanto_nao_ha_cobranca()
    {
        if (SemBanco) return;
        var admin = Admin();
        var e = await DadosDeTeste.CriarAsync(admin, "ReabrirG6");

        var reabrir = await Chamar(admin, HttpMethod.Patch,
            $"/api/campeonatos/{e.CampeonatoId}/temporadas/{e.TemporadaId}/inscricoes-habilitadas", new { habilitadas = true });

        reabrir.Status.Should().Be(HttpStatusCode.NoContent, reabrir.Corpo);
    }

    [Fact]
    public async Task Encerrar_a_temporada_com_jogo_agendado_e_recusado_e_iniciar_funciona()
    {
        if (SemBanco) return;
        var admin = Admin();
        var e = await DadosDeTeste.CriarAsync(admin, "EncerrarG6"); // tabela de jogos gerada

        (await Chamar(admin, HttpMethod.Patch, $"/api/campeonatos/{e.CampeonatoId}/temporadas/{e.TemporadaId}/iniciar")).Status.Should().Be(HttpStatusCode.NoContent);
        var encerrar = await Chamar(admin, HttpMethod.Patch, $"/api/campeonatos/{e.CampeonatoId}/temporadas/{e.TemporadaId}/encerrar");

        encerrar.Status.Should().Be(HttpStatusCode.BadRequest);
        encerrar.Corpo.Should().Contain("Há 1 jogo ainda não encerrado");
    }

    [Fact]
    public async Task Datas_da_competicao_sao_opcionais_na_criacao_e_na_edicao()
    {
        if (SemBanco) return;
        var admin = Admin();
        var e = await DadosDeTeste.CriarAsync(admin, "DatasG6"); // criada sem as datas da competição

        var inicial = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{e.TemporadaId}")).Corpo).RootElement;
        inicial.GetProperty("dataInicioCompeticao").ValueKind.Should().Be(JsonValueKind.Null);
        inicial.GetProperty("dataFimCompeticao").ValueKind.Should().Be(JsonValueKind.Null);

        var ok = await Chamar(admin, HttpMethod.Patch, $"/api/temporadas/{e.TemporadaId}/datas", new
        {
            dataInicioInscricoes = "2026-01-01", dataFimInscricoes = "2026-02-01", dataInicioCompeticao = "2026-03-01", dataFimCompeticao = "2026-06-30",
        });
        ok.Status.Should().Be(HttpStatusCode.NoContent, ok.Corpo);
        var depois = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{e.TemporadaId}")).Corpo).RootElement;
        depois.GetProperty("dataInicioCompeticao").GetString().Should().Be("2026-03-01");
        depois.GetProperty("dataFimCompeticao").GetString().Should().Be("2026-06-30");

        var invalida = await Chamar(admin, HttpMethod.Patch, $"/api/temporadas/{e.TemporadaId}/datas", new
        {
            dataInicioInscricoes = "2026-01-01", dataFimInscricoes = "2026-02-01", dataInicioCompeticao = "2026-06-30", dataFimCompeticao = "2026-03-01",
        });
        invalida.Status.Should().Be(HttpStatusCode.BadRequest);

        var limpar = await Chamar(admin, HttpMethod.Patch, $"/api/temporadas/{e.TemporadaId}/datas", new
        {
            dataInicioInscricoes = "2026-01-01", dataFimInscricoes = "2026-02-01",
        });
        limpar.Status.Should().Be(HttpStatusCode.NoContent);
        JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{e.TemporadaId}")).Corpo).RootElement
            .GetProperty("dataInicioCompeticao").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Criar_temporada_aceita_as_datas_da_competicao()
    {
        if (SemBanco) return;
        var admin = Admin();
        var e = await DadosDeTeste.CriarAsync(admin, "CriarDatas");

        var criar = await Chamar(admin, HttpMethod.Post, $"/api/campeonatos/{e.CampeonatoId}/temporadas", new
        {
            ano = DateTime.UtcNow.Year + 1, dataInicioInscricoes = DateTime.UtcNow, dataFimInscricoes = DateTime.UtcNow.AddDays(20),
            dataInicioCompeticao = "2027-03-01", dataFimCompeticao = "2027-06-30",
        });

        criar.Status.Should().Be(HttpStatusCode.NoContent, criar.Corpo);
        var camp = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/campeonatos/{e.CampeonatoId}")).Corpo).RootElement;
        camp.GetProperty("temporadas").EnumerateArray().Any(t => t.GetProperty("dataInicioCompeticao").GetString() == "2027-03-01").Should().BeTrue();
    }

    [Fact]
    public async Task Administrador_de_associacao_so_ve_o_resumo_da_propria_associacao()
    {
        if (SemBanco) return;
        var admin = Admin();
        var e = await DadosDeTeste.CriarAsync(admin, "EscopoG6");
        var dono = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", e.AssociacaoId));
        var outro = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", Guid.NewGuid()));

        (await Chamar(dono, HttpMethod.Get, $"/api/temporadas/{e.TemporadaId}/resumo")).Status.Should().Be(HttpStatusCode.OK);
        (await Chamar(outro, HttpMethod.Get, $"/api/temporadas/{e.TemporadaId}/resumo")).Status.Should().NotBe(HttpStatusCode.OK);
        (await Chamar(outro, HttpMethod.Patch, $"/api/temporadas/{e.TemporadaId}/datas", new { dataInicioInscricoes = "2026-01-01", dataFimInscricoes = "2026-02-01" })).Status.Should().NotBe(HttpStatusCode.OK);
        (await Chamar(_fabrica.ClienteCom(null), HttpMethod.Get, $"/api/temporadas/{e.TemporadaId}/resumo")).Status.Should().Be(HttpStatusCode.Unauthorized);
    }
}
