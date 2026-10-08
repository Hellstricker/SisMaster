using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SisMaster.WebApps.WebApi.Data;
using SisMaster.WebApps.WebApi.Data.Repositories;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;
using Xunit;

namespace SisMaster.WebApps.Tests.Importacao.Fiba;

/// <summary>
/// Grava de verdade num banco temporário (o SQL Server de desenvolvimento, aplicando as migrações): a troca de eventos/trocas na mesma gravação
/// e o JSON bruto em table splitting. Sem SQL Server, os testes falham (SISMASTER_TESTE_SEM_BANCO=1 os dispensa).
/// </summary>
public class ImportacaoPersistenciaTests : IAsyncLifetime
{
    private readonly string _banco = $"SisMasterTeste_{Guid.NewGuid():N}";
    private readonly List<string> _sql = [];
    private bool _disponivel;

    /// <summary>SQL Server de desenvolvimento (o do docker-compose); troque por SISMASTER_TESTE_SQLSERVER (sem o Database).</summary>
    private static string Conexao(string banco) =>
        (Environment.GetEnvironmentVariable("SISMASTER_TESTE_SQLSERVER")
            ?? "Server=localhost,1433;User Id=sa;Password=SisMaster@2026!;TrustServerCertificate=True") + $";Database={banco}";

    private CampeonatoDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<CampeonatoDbContext>()
            .UseSqlServer(Conexao(_banco))
            .LogTo(s => { lock (_sql) _sql.Add(s); }, Microsoft.Extensions.Logging.LogLevel.Information)
            .Options);

    public async Task InitializeAsync()
    {
        try
        {
            await using var ctx = NovoContexto();
            await ctx.Database.MigrateAsync();
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

    private static Guid J(Sumula s, LadoTime lado, int camisa) =>
        s.TimeDoLado(lado).Jogadores.First(j => j.Numero == camisa.ToString()).Id;

    private static Sumula NovaSumula()
    {
        var s = new Sumula(Guid.NewGuid(), Guid.NewGuid(), "Águias", null, Guid.NewGuid(), "Ursos", null);
        foreach (var (lado, primeira) in new[] { (LadoTime.Casa, 1), (LadoTime.Visitante, 11) })
        {
            var rel = Enumerable.Range(0, 7)
                .Select(i => new RelacionadoNovo(Guid.NewGuid(), $"Atleta {primeira + i}", (primeira + i).ToString(), i < 5)).ToList();
            s.SalvarRelacao(lado, "Prof.", null, rel, rel[0].AtletaId);
        }
        s.Iniciar();
        return s;
    }

    private static async Task Importar(CampeonatoDbContext ctx, Guid sumulaId, string json, int camisaQueMarca, int camisaQueSai, int camisaQueEntra)
    {
        var repo = new SumulaRepository(ctx);
        var s = (await repo.ObterPorIdAsync(sumulaId))!;
        var resultado = s.ImportarJogoRealizado("2888968",
            [new EventoImportado(J(s, LadoTime.Casa, camisaQueMarca), TipoEvento.Ponto2, Periodo.Primeiro, 300)],
            [new TrocaImportada(J(s, LadoTime.Casa, camisaQueSai), J(s, LadoTime.Casa, camisaQueEntra), Periodo.Segundo, 600)]);
        repo.AplicarImportacao(resultado);
        await repo.GuardarDadosExternosAsync(s.Id, json);
        await repo.UnitOfWork.Commit();
    }

    [Fact]
    public async Task Importa_substitui_na_reimportacao_e_guarda_o_json_bruto_sem_carregar_nas_leituras_normais()
    {
        if (!_disponivel) return;

        Guid id;
        await using (var ctx = NovoContexto())
        {
            var s = NovaSumula();
            id = s.Id;
            var repo = new SumulaRepository(ctx);
            repo.Adicionar(s);
            repo.AdicionarEvento(s.RegistrarEvento(J(s, LadoTime.Casa, 1), TipoEvento.Ponto3, 500)); // da mesa; será descartado
            await repo.UnitOfWork.Commit();
        }

        await using (var ctx = NovoContexto()) await Importar(ctx, id, "{\"v\":1}", 2, 1, 6);
        await using (var ctx = NovoContexto()) await Importar(ctx, id, "{\"v\":2}", 3, 2, 7); // reimporta: mesma Ordem 1 da troca

        _sql.Clear();
        await using (var ctx = NovoContexto())
        {
            var repo = new SumulaRepository(ctx);
            var s = (await repo.ObterPorIdAsync(id))!;

            s.Status.Should().Be(StatusSumula.Encerrada);
            s.CodigoExterno.Should().Be("2888968");
            s.PlacarCasa.Should().Be(2);
            s.Eventos.Should().ContainSingle().Which.JogadorId.Should().Be(J(s, LadoTime.Casa, 3));
            s.Substituicoes.Should().ContainSingle().Which.JogadorEntraId.Should().Be(J(s, LadoTime.Casa, 7));
        }
        _sql.Where(l => l.Contains("FROM [Sumulas]")).Should().NotBeEmpty();
        _sql.Should().NotContain(l => l.Contains("DadosExternosJson")); // a leitura normal não toca o JSON

        await using (var ctx = NovoContexto())
        {
            (await new SumulaRepository(ctx).ObterDadosExternosAsync(id)).Should().Be("{\"v\":2}");
        }
    }

    [Fact]
    public async Task Sumula_sem_importacao_nao_tem_dados_externos()
    {
        if (!_disponivel) return;

        Guid id;
        await using (var ctx = NovoContexto())
        {
            var s = NovaSumula();
            id = s.Id;
            var repo = new SumulaRepository(ctx);
            repo.Adicionar(s);
            await repo.UnitOfWork.Commit();
        }

        await using (var ctx = NovoContexto())
        {
            (await new SumulaRepository(ctx).ObterDadosExternosAsync(id)).Should().BeNull();
        }
    }
}
