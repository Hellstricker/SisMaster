using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
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
/// O comando inteiro, com o banco de verdade e o feed do jogo 2888968 (arquivo): relação igual à do feed, jogo e súmula gravados,
/// importação aplicada. Cobre o que os testes do domínio não cobrem: camisa → jogador, jogo encerrado, trava de duplicidade.
/// </summary>
public class ImportarFibaHandlerTests : IAsyncLifetime
{
    private readonly string _banco = $"SisMasterTeste_{Guid.NewGuid():N}";
    private bool _disponivel;

    private static readonly JsonSerializerOptions Opcoes = new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };
    private static readonly string Json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Importacao", "Fiba", "jogo-2888968.json"));

    private sealed class FeedFalso(string json) : IFibaLiveStatsClient
    {
        public Task<FibaFeed> ObterJogoAsync(string codigoJogo, CancellationToken ct = default) =>
            Task.FromResult(new FibaFeed(JsonSerializer.Deserialize<FibaJogoDto>(json, Opcoes)!, json));
    }

    private CampeonatoDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<CampeonatoDbContext>()
            .UseSqlServer((Environment.GetEnvironmentVariable("SISMASTER_TESTE_SQLSERVER")
                ?? "Server=localhost,1433;User Id=sa;Password=SisMaster@2026!;TrustServerCertificate=True") + $";Database={_banco}")
            .Options);

    public async Task InitializeAsync()
    {
        try
        {
            await using var ctx = NovoContexto();
            await ctx.Database.MigrateAsync();
            // Só o jogo e a súmula interessam aqui: sem montar a hierarquia (associação → temporada → fase) que as FKs exigiriam.
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

    private static ImportarFibaCommandHandler NovoHandler(CampeonatoDbContext ctx, string json)
    {
        var sumulas = new SumulaRepository(ctx);
        var jogos = new JogoRepository(ctx);
        var hub = new Mock<IHubContext<SumulaHub>>();
        hub.Setup(h => h.Clients.Group(It.IsAny<string>()).SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new ImportarFibaCommandHandler(
            new ImportacaoFibaPreviaQuery(sumulas, new FeedFalso(json)), sumulas, jogos,
            new AvancoDaTemporada(jogos, sumulas, new BonificacaoDosJogos(sumulas)), hub.Object);
    }

    /// <summary>Jogo agendado + súmula em preparação com a relação igual à do feed (Croácia em casa); inverter troca os lados.</summary>
    private static async Task<(Guid JogoId, Guid SumulaId)> CriarJogoEmPreparacao(CampeonatoDbContext ctx, bool inverter = false)
    {
        var feed = JsonSerializer.Deserialize<FibaJogoDto>(Json, Opcoes)!;
        var faseId = Guid.NewGuid();
        var casa = new FaseEquipe(faseId, null, 1, ReferenciaEquipe.DeColocacao(faseId, null, 1));
        var vis = new FaseEquipe(faseId, null, 2, ReferenciaEquipe.DeColocacao(faseId, null, 2));
        var jogo = new Jogo(Guid.NewGuid(), faseId, null, null, 1, null, false, casa, vis);

        var sumula = new Sumula(jogo.Id, Guid.NewGuid(), "Croácia", null, Guid.NewGuid(), "Panther", null);
        foreach (var (lado, chave) in new[] { (LadoTime.Casa, inverter ? "2" : "1"), (LadoTime.Visitante, inverter ? "1" : "2") })
        {
            var rel = feed.Tm[chave].Pl.Values
                .Select(p => new RelacionadoNovo(Guid.NewGuid(), p.NomeCompleto, p.ShirtNumber, p.Starter == 1)).ToList();
            sumula.SalvarRelacao(lado, "Prof.", null, rel, rel[0].AtletaId);
        }
        jogo.VincularSumula(sumula.Id);

        ctx.Jogos.Add(jogo);
        new SumulaRepository(ctx).Adicionar(sumula);
        await ctx.SaveChangesAsync();
        return (jogo.Id, sumula.Id);
    }

    [Fact]
    public async Task Importa_o_jogo_inteiro_encerra_jogo_e_sumula_e_guarda_o_json()
    {
        if (!_disponivel) return;

        Guid jogoId, sumulaId;
        await using (var ctx = NovoContexto()) (jogoId, sumulaId) = await CriarJogoEmPreparacao(ctx);

        await using (var ctx = NovoContexto())
        {
            var r = await NovoHandler(ctx, Json).Handle(new ImportarFibaCommand { SumulaId = sumulaId, Codigo = "2888968" }, default);
            r.Errors.Should().BeEmpty();
        }

        await using (var ctx = NovoContexto())
        {
            var s = (await new SumulaRepository(ctx).ObterPorIdAsync(sumulaId))!;
            s.Status.Should().Be(StatusSumula.Encerrada);
            s.CodigoExterno.Should().Be("2888968");
            (s.PlacarCasa, s.PlacarVisitante).Should().Be((50, 68));
            s.Eventos.Should().HaveCount(373);
            s.Substituicoes.Should().HaveCount(24);
            s.Substituicoes.Select(x => x.Ordem).Should().BeEquivalentTo(Enumerable.Range(1, 24));

            var jogo = (await new JogoRepository(ctx).ObterPorIdAsync(jogoId))!;
            jogo.Status.Should().Be(StatusJogo.Encerrado);
            (jogo.PlacarCasa, jogo.PlacarVisitante).Should().Be((50, 68));
            jogo.SumulaId.Should().Be(sumulaId);

            (await new SumulaRepository(ctx).ObterDadosExternosAsync(sumulaId)).Should().Be(Json);
        }
    }

    [Fact]
    public async Task Reimportar_na_mesma_sumula_refaz_e_importar_o_mesmo_jogo_em_outra_e_recusado()
    {
        if (!_disponivel) return;

        Guid sumulaA, sumulaB;
        await using (var ctx = NovoContexto()) (_, sumulaA) = await CriarJogoEmPreparacao(ctx);
        await using (var ctx = NovoContexto()) (_, sumulaB) = await CriarJogoEmPreparacao(ctx);

        await using (var ctx = NovoContexto())
            (await NovoHandler(ctx, Json).Handle(new ImportarFibaCommand { SumulaId = sumulaA, Codigo = "2888968" }, default)).Errors.Should().BeEmpty();
        await using (var ctx = NovoContexto())
            (await NovoHandler(ctx, Json).Handle(new ImportarFibaCommand { SumulaId = sumulaA, Codigo = "2888968" }, default)).Errors.Should().BeEmpty();

        await using (var ctx = NovoContexto())
        {
            var r = await NovoHandler(ctx, Json).Handle(new ImportarFibaCommand { SumulaId = sumulaB, Codigo = "2888968" }, default);
            r.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Contain("já foi importado em outra súmula");
        }
        await using (var ctx = NovoContexto())
        {
            var a = (await new SumulaRepository(ctx).ObterPorIdAsync(sumulaA))!;
            a.Eventos.Should().HaveCount(373);
            var b = (await new SumulaRepository(ctx).ObterPorIdAsync(sumulaB))!;
            b.Status.Should().Be(StatusSumula.EmPreparacao);
            b.CodigoExterno.Should().BeNull();
        }
    }

    [Fact]
    public async Task Conferencia_que_nao_fecha_nao_grava_nada()
    {
        if (!_disponivel) return;

        Guid jogoId, sumulaId;
        await using (var ctx = NovoContexto()) (jogoId, sumulaId) = await CriarJogoEmPreparacao(ctx, inverter: true); // lados trocados

        await using (var ctx = NovoContexto())
        {
            var r = await NovoHandler(ctx, Json).Handle(new ImportarFibaCommand { SumulaId = sumulaId, Codigo = "2888968" }, default);
            r.Errors.Should().NotBeEmpty();
        }

        await using (var ctx = NovoContexto())
        {
            var s = (await new SumulaRepository(ctx).ObterPorIdAsync(sumulaId))!;
            s.Status.Should().Be(StatusSumula.EmPreparacao);
            s.Eventos.Should().BeEmpty();
            (await new JogoRepository(ctx).ObterPorIdAsync(jogoId))!.Status.Should().Be(StatusJogo.Agendado);
            (await new SumulaRepository(ctx).ObterDadosExternosAsync(sumulaId)).Should().BeNull();
        }
    }

    [Fact]
    public async Task Codigo_invalido_e_recusado_sem_ir_ao_feed()
    {
        if (!_disponivel) return;

        await using var ctx = NovoContexto();
        var (_, sumulaId) = await CriarJogoEmPreparacao(ctx);

        var r = await NovoHandler(ctx, Json).Handle(new ImportarFibaCommand { SumulaId = sumulaId, Codigo = "https://x/u/CBBC/2888968/bs.html" }, default);

        r.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Contain("código do jogo");
    }
}
