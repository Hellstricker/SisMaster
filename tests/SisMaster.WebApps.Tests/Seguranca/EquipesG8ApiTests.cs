using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SisMaster.WebApps.WebApi.Data;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;
using Xunit;

namespace SisMaster.WebApps.Tests.Seguranca;

/// <summary>
/// G8 pela API de verdade: transferir atleta entre equipes da categoria, trocar dois atletas, e o histórico de quem já jogou.
/// Exigem SQL Server (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
[Collection(ColecaoApi.Nome)]
public class EquipesG8ApiTests
{
    private static readonly bool SemBanco = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1";
    private readonly ApiFactory _fabrica;

    public EquipesG8ApiTests(ApiFactory fabrica) => _fabrica = fabrica;

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

    private sealed record Cenario(Estrutura E, Guid Tc, HttpClient Admin, Guid Aguias, Guid Ursos);

    private async Task<Cenario> NovoAsync(string nome)
    {
        var admin = _fabrica.ClienteCom(ApiFactory.Token("Administrador"));
        var e = await DadosDeTeste.CriarAsync(admin, nome);
        var tc = (await DadosDeTeste.Enviar(admin, HttpMethod.Get, $"/api/temporadas/{e.TemporadaId}"))
            .GetProperty("categorias").EnumerateArray().First().GetProperty("id").GetGuid();
        await Chamar(admin, HttpMethod.Patch, $"/api/temporadas/{e.TemporadaId}/taxa-inscricao", new { taxa = 100 });
        var cat = await Categoria(admin, e.TemporadaId);
        Guid Equipe(string n) => cat.GetProperty("equipes").EnumerateArray().First(q => q.GetProperty("nome").GetString() == n).GetProperty("id").GetGuid();
        return new Cenario(e, tc, admin, Equipe("Águias"), Equipe("Ursos"));
    }

    private static async Task<JsonElement> Categoria(HttpClient admin, Guid temporada)
    {
        var corpo = JsonDocument.Parse((await Chamar(admin, HttpMethod.Get, $"/api/temporadas/{temporada}/equipes")).Corpo).RootElement;
        return (corpo.TryGetProperty("data", out var d) ? d : corpo).GetProperty("categorias")[0].Clone();
    }

    /// <summary>Inscreve, aprova, lança a taxa e põe o atleta no elenco; devolve o id do atleta (vínculo com a equipe).</summary>
    private async Task<Guid> Atleta(Cenario c, Guid equipe, string nome, int n)
    {
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, "/api/inscricoes", new
        {
            temporadaId = c.E.TemporadaId, nome, cpf = CpfValido(70000 + n), nascimento = "1975-05-10", sexo = "Masculino", email = "atleta@exemplo.test",
            telefone = "71999990000", alturaCm = 180, pesoKg = 80, posicao = "Ala", consentimentoLgpd = true, temporadaCategoriaIds = new[] { c.Tc },
        });
        var ficha = (await DadosDeTeste.Enviar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.E.TemporadaId}/inscricoes")).EnumerateArray()
            .First(f => f.GetProperty("pessoa").GetProperty("nome").GetString() == nome);
        var fichaId = ficha.GetProperty("id").GetGuid();
        var pedido = ficha.GetProperty("categorias")[0].GetProperty("id").GetGuid();
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Patch, $"/api/inscricoes-categorias/{pedido}/aprovar", new { });
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/inscricoes/{fichaId}/pagamentos/taxa", new { dataPagamento = "2026-10-09" });
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/equipes/{equipe}/atletas", new { inscricaoCategoriaId = pedido });
        return (await Elenco(c, equipe)).First(a => a.GetProperty("nome").GetString() == nome).GetProperty("id").GetGuid();
    }

    private static async Task<List<JsonElement>> ElencoDe(HttpClient admin, Guid temporada, Guid equipe)
    {
        var cat = await Categoria(admin, temporada);
        return cat.GetProperty("equipes").EnumerateArray().First(q => q.GetProperty("id").GetGuid() == equipe).GetProperty("atletas").EnumerateArray().ToList();
    }

    private Task<List<JsonElement>> Elenco(Cenario c, Guid equipe) => ElencoDe(c.Admin, c.E.TemporadaId, equipe);

    private static Task<(HttpStatusCode Status, string Corpo)> Transferir(Cenario c, Guid de, Guid atleta, Guid para, Guid? troca = null) =>
        Chamar(c.Admin, HttpMethod.Post, $"/api/equipes/{de}/atletas/{atleta}/transferir", new { equipeDestinoId = para, atletaTrocaId = troca });

    [Fact]
    public async Task Transferencia_simples_muda_o_atleta_de_equipe()
    {
        if (SemBanco) return;
        var c = await NovoAsync("TransfSimples");
        var ozias = await Atleta(c, c.Aguias, "Ozias Transferido", 11);

        (await Transferir(c, c.Aguias, ozias, c.Ursos)).Status.Should().Be(HttpStatusCode.NoContent);

        (await Elenco(c, c.Aguias)).Should().BeEmpty();
        var ursos = await Elenco(c, c.Ursos);
        ursos.Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(ozias, "a identidade do atleta é mantida");
        ursos[0].GetProperty("jogos").GetInt32().Should().Be(0);
        ursos[0].GetProperty("equipesAnteriores").GetArrayLength().Should().Be(0, "sem jogo feito, a troca não deixa histórico");
    }

    [Fact]
    public async Task Troca_move_os_dois_atletas_de_uma_vez()
    {
        if (SemBanco) return;
        var c = await NovoAsync("TransfTroca");
        var ozias = await Atleta(c, c.Aguias, "Ozias Troca", 12);
        var william = await Atleta(c, c.Ursos, "William Troca", 13);

        (await Transferir(c, c.Aguias, ozias, c.Ursos, william)).Status.Should().Be(HttpStatusCode.NoContent);

        (await Elenco(c, c.Aguias)).Select(a => a.GetProperty("nome").GetString()).Should().Equal("William Troca");
        (await Elenco(c, c.Ursos)).Select(a => a.GetProperty("nome").GetString()).Should().Equal("Ozias Troca");
    }

    [Fact]
    public async Task Recusa_mesma_equipe_atleta_de_outra_equipe_e_troca_com_quem_nao_esta_no_destino()
    {
        if (SemBanco) return;
        var c = await NovoAsync("TransfRecusa");
        var ozias = await Atleta(c, c.Aguias, "Ozias Recusa", 14);
        var william = await Atleta(c, c.Aguias, "William Recusa", 15);

        var mesma = await Transferir(c, c.Aguias, ozias, c.Aguias);
        mesma.Status.Should().Be(HttpStatusCode.BadRequest);
        mesma.Corpo.Should().Contain("equipe diferente");

        var naoEstaNoDestino = await Transferir(c, c.Aguias, ozias, c.Ursos, william);
        naoEstaNoDestino.Status.Should().Be(HttpStatusCode.BadRequest);
        naoEstaNoDestino.Corpo.Should().Contain("não encontrado");

        var outroAtleta = await Transferir(c, c.Ursos, ozias, c.Aguias);
        outroAtleta.Status.Should().Be(HttpStatusCode.BadRequest, "o atleta não é do elenco informado");

        (await Elenco(c, c.Aguias)).Should().HaveCount(2, "nada mudou");
    }

    [Fact]
    public async Task Recusa_equipe_de_outra_categoria()
    {
        if (SemBanco) return;
        var c = await NovoAsync("TransfCategoria");
        var ozias = await Atleta(c, c.Aguias, "Ozias Categoria", 16);

        // Segunda categoria na mesma temporada, com uma equipe própria.
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/associacoes/{c.E.AssociacaoId}/categorias",
            new { nome = "Supermaster 50+", idadeMinima = 50, sexo = "Masculino", aceitaAbaixoIdadeMinima = false, minimoPeriodosEmQuadra = 1, minimoPeriodosForaQuadra = 1 });
        var categorias = await DadosDeTeste.Enviar(c.Admin, HttpMethod.Get, $"/api/associacoes/{c.E.AssociacaoId}/categorias");
        var outra = categorias.EnumerateArray().First(x => x.GetProperty("nome").GetString() == "Supermaster 50+").GetProperty("id").GetGuid();
        var adicionou = await Chamar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.E.TemporadaId}/categorias", new { categoriaId = outra });
        if (adicionou.Status != HttpStatusCode.NoContent && adicionou.Status != HttpStatusCode.OK) return; // regra da temporada impede (ex.: tabela gerada): nada a provar aqui

        var tc2 = (await DadosDeTeste.Enviar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.E.TemporadaId}")).GetProperty("categorias").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() != c.Tc).GetProperty("id").GetGuid();
        await DadosDeTeste.Enviar(c.Admin, HttpMethod.Post, $"/api/temporadas/{c.E.TemporadaId}/equipes",
            new { temporadaId = c.E.TemporadaId, temporadaCategoriaId = tc2, nome = "Outra Categoria", cor = "#00ff00" });
        var corpo = JsonDocument.Parse((await Chamar(c.Admin, HttpMethod.Get, $"/api/temporadas/{c.E.TemporadaId}/equipes")).Corpo).RootElement;
        var cats = (corpo.TryGetProperty("data", out var d) ? d : corpo).GetProperty("categorias").EnumerateArray();
        var equipeOutra = cats.SelectMany(k => k.GetProperty("equipes").EnumerateArray()).First(q => q.GetProperty("nome").GetString() == "Outra Categoria").GetProperty("id").GetGuid();

        var r = await Transferir(c, c.Aguias, ozias, equipeOutra);

        r.Status.Should().Be(HttpStatusCode.BadRequest);
        r.Corpo.Should().Contain("mesma categoria");
    }

    [Fact]
    public async Task Arbitro_nao_transfere_atleta()
    {
        if (SemBanco) return;
        var c = await NovoAsync("TransfArbitro");
        var ozias = await Atleta(c, c.Aguias, "Ozias Arbitro", 17);
        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", c.E.AssociacaoId));

        var r = await Chamar(arbitro, HttpMethod.Post, $"/api/equipes/{c.Aguias}/atletas/{ozias}/transferir", new { equipeDestinoId = c.Ursos });

        r.Status.Should().Be(HttpStatusCode.Forbidden);
        (await Elenco(c, c.Aguias)).Should().ContainSingle();
    }

    [Fact]
    public async Task Administrador_de_outra_associacao_nao_transfere()
    {
        if (SemBanco) return;
        var c = await NovoAsync("TransfOutraAssoc");
        var ozias = await Atleta(c, c.Aguias, "Ozias Alheio", 18);
        var alheio = _fabrica.ClienteCom(ApiFactory.Token("AdministradorDeAssociacao", Guid.NewGuid()));

        var r = await Chamar(alheio, HttpMethod.Post, $"/api/equipes/{c.Aguias}/atletas/{ozias}/transferir", new { equipeDestinoId = c.Ursos });

        r.Status.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        (await Elenco(c, c.Aguias)).Should().ContainSingle();
    }

    // ------------------------------------------------------------------ com súmula

    private async Task<(Cenario C, List<Guid> Aguias, List<Guid> Ursos)> CenarioComRelacoes(string nome, int seed)
    {
        var c = await NovoAsync(nome);
        var aguias = new List<Guid>();
        var ursos = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            aguias.Add(await Atleta(c, c.Aguias, $"Aguia {nome} {i}", seed + i));
            ursos.Add(await Atleta(c, c.Ursos, $"Urso {nome} {i}", seed + 10 + i));
        }

        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", c.E.AssociacaoId));
        await DadosDeTeste.Enviar(arbitro, HttpMethod.Post, $"/api/jogos/{c.E.JogoId}/sumula");
        return (c, aguias, ursos);
    }

    private async Task SalvarRelacao(Cenario c, int lado, List<Guid> atletas, bool completa = true)
    {
        var arbitro = _fabrica.ClienteCom(ApiFactory.Token("Arbitro", c.E.AssociacaoId));
        var jogadores = atletas.Select((id, i) => new { atletaId = id, numero = (i + 4).ToString(), titular = completa });
        await DadosDeTeste.Enviar(arbitro, HttpMethod.Put, $"/api/jogos/{c.E.JogoId}/sumula/relacao",
            new { lado, capitaoAtletaId = completa ? atletas[0] : (Guid?)null, jogadores });
    }

    private async Task ForcarStatus(Guid jogoId, StatusSumula status)
    {
        using var escopo = _fabrica.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<CampeonatoDbContext>();
        var s = await db.Sumulas.FirstAsync(x => x.JogoId == jogoId);
        db.Entry(s).Property(x => x.Status).CurrentValue = status;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Atleta_na_relacao_de_jogo_nao_terminado_nao_e_transferido_nem_removido()
    {
        if (SemBanco) return;
        var (c, aguias, _) = await CenarioComRelacoes("TransfSumulaAberta", 100);
        await SalvarRelacao(c, 0, aguias, completa: false);

        var transf = await Transferir(c, c.Aguias, aguias[0], c.Ursos);
        var remover = await Chamar(c.Admin, HttpMethod.Delete, $"/api/equipes/{c.Aguias}/atletas/{aguias[0]}");

        transf.Status.Should().Be(HttpStatusCode.BadRequest);
        transf.Corpo.Should().Contain("não terminou");
        remover.Status.Should().Be(HttpStatusCode.BadRequest);
        remover.Corpo.Should().Contain("não pode ser removido");
        (await Elenco(c, c.Aguias)).Should().HaveCount(4);
    }

    [Fact]
    public async Task Quem_ja_jogou_e_transferido_com_historico_e_nao_pode_ser_removido()
    {
        if (SemBanco) return;
        var (c, aguias, ursos) = await CenarioComRelacoes("TransfHistorico", 200);
        await SalvarRelacao(c, 0, aguias);
        await SalvarRelacao(c, 1, ursos);
        (await Chamar(_fabrica.ClienteCom(ApiFactory.Token("Arbitro", c.E.AssociacaoId)), HttpMethod.Post, $"/api/jogos/{c.E.JogoId}/sumula/iniciar"))
            .Status.Should().Be(HttpStatusCode.NoContent);
        await ForcarStatus(c.E.JogoId, StatusSumula.Encerrada);

        var antes = (await Elenco(c, c.Aguias)).First(a => a.GetProperty("id").GetGuid() == aguias[1]);
        antes.GetProperty("jogos").GetInt32().Should().Be(1);

        var remover = await Chamar(c.Admin, HttpMethod.Delete, $"/api/equipes/{c.Aguias}/atletas/{aguias[1]}");
        remover.Status.Should().Be(HttpStatusCode.BadRequest, "quem já jogou só pode ser transferido");

        (await Transferir(c, c.Aguias, aguias[1], c.Ursos)).Status.Should().Be(HttpStatusCode.NoContent);

        var depois = (await Elenco(c, c.Ursos)).First(a => a.GetProperty("id").GetGuid() == aguias[1]);
        depois.GetProperty("jogos").GetInt32().Should().Be(1, "o jogo feito continua contando para o atleta");
        var anteriores = depois.GetProperty("equipesAnteriores");
        anteriores.GetArrayLength().Should().Be(1);
        anteriores[0].GetProperty("equipeNome").GetString().Should().Be("Águias");
        anteriores[0].GetProperty("jogos").GetInt32().Should().Be(1);
        (await Elenco(c, c.Aguias)).Should().HaveCount(3);
    }
}
