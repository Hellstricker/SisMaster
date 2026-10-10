using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using SisMaster.WebApps.Tests.Infra;
using SisMaster.WebApps.WebApi.Application.Commands;
using SisMaster.WebApps.WebApi.Application.Commands.Handlers;
using SisMaster.WebApps.WebApi.Application.Importacao.Fiba;
using SisMaster.WebApps.WebApi.Application.Services;
using SisMaster.WebApps.WebApi.Data;
using SisMaster.WebApps.WebApi.Data.Repositories;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;
using SisMaster.WebApps.WebApi.Hubs;
using Xunit;

namespace SisMaster.WebApps.Tests.Importacao.Fiba;

/// <summary>
/// Um segundo jogo real do Master, agora com PRORROGAÇÃO: o data.json do jogo 2844703 (IRISH 70 × 62 TORONTO RAPTORS, 4 períodos e
/// 1 prorrogação, 22 trocas por equipe somadas). Confere a tradução, o placar total e por período (inclusive a prorrogação) e a
/// importação inteira com o banco de verdade.
/// </summary>
public class FibaProrrogacaoTests : IAsyncLifetime
{
    private readonly string _banco = $"SisMasterTeste_{Guid.NewGuid():N}";
    private bool _disponivel;

    private static readonly JsonSerializerOptions Opcoes = new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };
    private static readonly string Json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Importacao", "Fiba", "jogo-2844703.json"));
    private static FibaJogoDto Jogo() => JsonSerializer.Deserialize<FibaJogoDto>(Json, Opcoes)!;

    private static readonly IReadOnlyDictionary<LadoTime, string> Nomes =
        new Dictionary<LadoTime, string> { [LadoTime.Casa] = "Irish", [LadoTime.Visitante] = "Raptors" };

    private static List<RelacionadoDaSumula> RelacaoIgualAoFeed(FibaJogoDto jogo) =>
        jogo.Tm.SelectMany(kv => kv.Value.Pl.Values.Select(p =>
            new RelacionadoDaSumula(kv.Key == "1" ? LadoTime.Casa : LadoTime.Visitante, p.ShirtNumber, p.NomeCompleto, p.Starter == 1)))
            .ToList();

    // ------------------------------------------------------------------ tradução e conferência

    [Fact]
    public void Traduz_o_jogo_com_prorrogacao_sem_problemas()
    {
        var tr = FibaTradutor.Traduzir(Jogo());

        tr.Problemas.Should().BeEmpty();
        tr.NaoMapeados.Should().BeEmpty();
        tr.Trocas.Should().HaveCount(22);
        tr.Eventos.Select(e => e.Ordem).Should().BeInAscendingOrder();
        tr.Eventos.Should().Contain(e => e.Periodo == Periodo.Prorrogacao, "o jogo foi para a prorrogação");
        tr.Eventos.Select(e => e.Periodo).Should().BeInAscendingOrder("os lances saem na ordem em que aconteceram");
    }

    [Fact]
    public void O_relogio_da_prorrogacao_nunca_passa_de_5_minutos()
    {
        var tr = FibaTradutor.Traduzir(Jogo());

        var prorrogacao = tr.Eventos.Where(e => e.Periodo == Periodo.Prorrogacao).ToList();
        prorrogacao.Should().NotBeEmpty();
        prorrogacao.Should().OnlyContain(e => e.TempoRestanteSegundos >= 0 && e.TempoRestanteSegundos <= 300);
        tr.Eventos.Where(e => e.Periodo != Periodo.Prorrogacao).Should().OnlyContain(e => e.TempoRestanteSegundos >= 0 && e.TempoRestanteSegundos <= 600);
    }

    [Fact]
    public void Reconstroi_placar_total_e_por_periodo_inclusive_a_prorrogacao_igual_ao_oficial()
    {
        var jogo = Jogo();

        var previa = FibaAnalisador.Analisar(jogo, FibaTradutor.Traduzir(jogo), RelacaoIgualAoFeed(jogo), Nomes);

        previa.Problemas.Should().BeEmpty();
        previa.PodeAplicar.Should().BeTrue();
        previa.Times.Single(t => t.Lado == LadoTime.Casa).Should().Match<PreviaTimeFiba>(t => t.PlacarFeed == 70 && t.PlacarReconstruido == 70);
        previa.Times.Single(t => t.Lado == LadoTime.Visitante).Should().Match<PreviaTimeFiba>(t => t.PlacarFeed == 62 && t.PlacarReconstruido == 62);
        previa.Times.Should().OnlyContain(t => t.Periodos.Any(p => p.Periodo == (int)Periodo.Prorrogacao), "cada equipe tem o placar da prorrogação na conferência");
        previa.Times.SelectMany(t => t.Periodos).Should().OnlyContain(p => p.PlacarFeed == p.PlacarReconstruido);
        previa.Times.SelectMany(t => t.Jogadores).Should().OnlyContain(j => j.Divergencias.Count == 0);
    }

    // ------------------------------------------------------------------ importação inteira

    private CampeonatoDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<CampeonatoDbContext>().UseSqlServer(BancoDeTeste.ComBanco(_banco)).Options);

    public async Task InitializeAsync()
    {
        try
        {
            await using var ctx = NovoContexto();
            await ctx.Database.MigrateAsync();
            await ctx.Database.ExecuteSqlRawAsync("ALTER TABLE FaseEquipes NOCHECK CONSTRAINT ALL; ALTER TABLE Jogos NOCHECK CONSTRAINT ALL");
            _disponivel = true;
        }
        catch (Exception) when (Environment.GetEnvironmentVariable("SISMASTER_TESTE_SEM_BANCO") == "1") { _disponivel = false; }
    }

    public async Task DisposeAsync()
    {
        if (!_disponivel) return;
        await using var ctx = NovoContexto();
        await ctx.Database.EnsureDeletedAsync();
    }

    private sealed class FeedFalso(string json) : IFibaLiveStatsClient
    {
        public Task<FibaFeed> ObterJogoAsync(string codigoJogo, CancellationToken ct = default) =>
            Task.FromResult(new FibaFeed(JsonSerializer.Deserialize<FibaJogoDto>(json, Opcoes)!, json));
    }

    private static ImportarFibaCommandHandler NovoHandler(CampeonatoDbContext ctx)
    {
        var sumulas = new SumulaRepository(ctx);
        var jogos = new JogoRepository(ctx);
        var hub = new Mock<IHubContext<SumulaHub>>();
        hub.Setup(h => h.Clients.Group(It.IsAny<string>()).SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new ImportarFibaCommandHandler(
            new ImportacaoFibaPreviaQuery(sumulas, new FeedFalso(Json)), sumulas, jogos,
            new AvancoDaTemporada(jogos, sumulas, new BonificacaoDosJogos(sumulas)), hub.Object);
    }

    [Fact]
    public async Task Importa_o_jogo_com_prorrogacao_e_encerra_jogo_e_sumula_com_o_placar_oficial()
    {
        if (!_disponivel) return;

        var feed = Jogo();
        Guid jogoId, sumulaId;
        await using (var ctx = NovoContexto())
        {
            var faseId = Guid.NewGuid();
            var casa = new FaseEquipe(faseId, null, 1, ReferenciaEquipe.DeColocacao(faseId, null, 1));
            var vis = new FaseEquipe(faseId, null, 2, ReferenciaEquipe.DeColocacao(faseId, null, 2));
            var jogo = new Jogo(Guid.NewGuid(), faseId, null, null, 1, null, false, casa, vis);
            var sumula = new Sumula(jogo.Id, Guid.NewGuid(), "Irish", null, Guid.NewGuid(), "Raptors", null);
            foreach (var (lado, chave) in new[] { (LadoTime.Casa, "1"), (LadoTime.Visitante, "2") })
            {
                var rel = feed.Tm[chave].Pl.Values.Select(p => new RelacionadoNovo(Guid.NewGuid(), p.NomeCompleto, p.ShirtNumber, p.Starter == 1)).ToList();
                sumula.SalvarRelacao(lado, "Prof.", null, rel, rel[0].AtletaId);
            }
            jogo.VincularSumula(sumula.Id);
            ctx.Jogos.Add(jogo);
            new SumulaRepository(ctx).Adicionar(sumula);
            await ctx.SaveChangesAsync();
            (jogoId, sumulaId) = (jogo.Id, sumula.Id);
        }

        await using (var ctx = NovoContexto())
        {
            var r = await NovoHandler(ctx).Handle(new ImportarFibaCommand { SumulaId = sumulaId, Codigo = "2844703" }, default);
            r.Errors.Should().BeEmpty();
        }

        await using (var ctx = NovoContexto())
        {
            var s = (await new SumulaRepository(ctx).ObterPorIdAsync(sumulaId))!;
            s.Status.Should().Be(StatusSumula.Encerrada);
            s.CodigoExterno.Should().Be("2844703");
            (s.PlacarCasa, s.PlacarVisitante).Should().Be((70, 62));
            s.PeriodoAtual.Should().Be(Periodo.Prorrogacao, "o jogo terminou na prorrogação");
            s.Substituicoes.Should().HaveCount(22);
            s.Eventos.Should().Contain(e => e.Periodo == Periodo.Prorrogacao);

            var jogo = (await new JogoRepository(ctx).ObterPorIdAsync(jogoId))!;
            jogo.Status.Should().Be(StatusJogo.Encerrado);
            (jogo.PlacarCasa, jogo.PlacarVisitante).Should().Be((70, 62));
            (await new SumulaRepository(ctx).ObterDadosExternosAsync(sumulaId)).Should().Be(Json);
        }
    }
}
