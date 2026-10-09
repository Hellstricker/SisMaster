using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using SisMaster.WebApps.WebApi.Application.Importacao.Fiba;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;
using Xunit;

namespace SisMaster.WebApps.Tests.Importacao.Fiba;

/// <summary>Usa o data.json real do jogo 2888968 (CROÁCIA 50 x 68 PANTHER, Master 2026).</summary>
public class FibaImportacaoTests
{
    private static readonly JsonSerializerOptions Opcoes = new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    private static FibaJogoDto Jogo() =>
        JsonSerializer.Deserialize<FibaJogoDto>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Importacao", "Fiba", "jogo-2888968.json")), Opcoes)!;

    /// <summary>Relação da súmula idêntica ao feed (todos os jogadores, com os mesmos titulares).</summary>
    private static List<RelacionadoDaSumula> RelacaoIgualAoFeed(FibaJogoDto jogo, bool inverter = false) =>
        jogo.Tm.SelectMany(kv => kv.Value.Pl.Values.Select(p =>
            new RelacionadoDaSumula((kv.Key == "1") != inverter ? LadoTime.Casa : LadoTime.Visitante, p.ShirtNumber, p.NomeCompleto, p.Starter == 1)))
            .ToList();

    private static readonly IReadOnlyDictionary<LadoTime, string> Nomes =
        new Dictionary<LadoTime, string> { [LadoTime.Casa] = "Croácia", [LadoTime.Visitante] = "Panther" };

    [Fact]
    public void Traduz_o_jogo_real_sem_problemas()
    {
        var tr = FibaTradutor.Traduzir(Jogo());

        tr.Problemas.Should().BeEmpty();
        tr.NaoMapeados.Should().BeEmpty();
        tr.EventosDeEquipeIgnorados.Should().BeGreaterThan(0); // rebotes e turnovers de equipe
        tr.Trocas.Should().HaveCount(24);
        tr.Eventos.Select(e => e.Ordem).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Lista_os_lances_de_equipe_para_a_conferencia()
    {
        var jogo = Jogo();
        var previa = FibaAnalisador.Analisar(jogo, FibaTradutor.Traduzir(jogo), RelacaoIgualAoFeed(jogo), Nomes);

        previa.EventosDeEquipe.Should().HaveCount(16);
        previa.EventosDeEquipe.Count(e => e.Tipo == TipoEvento.Turnover).Should().Be(2);
        previa.EventosDeEquipe.Count(e => e.Tipo == TipoEvento.ReboteDefensivo).Should().Be(9);
        previa.EventosDeEquipe.Count(e => e.Tipo == TipoEvento.ReboteOfensivo).Should().Be(5);
        previa.EventosDeEquipe.Count(e => e.Lado == LadoTime.Casa).Should().Be(8);
        previa.EventosDeEquipe.Should().BeInAscendingOrder(e => e.Periodo); // na ordem em que aconteceram
        previa.EventosDeEquipe.First().Should().Be(new EventoDeEquipeFiba(LadoTime.Casa, TipoEvento.ReboteDefensivo, Periodo.Primeiro, 188));
    }

    [Fact]
    public void Reconstroi_placar_total_e_por_periodo_igual_ao_oficial()
    {
        var jogo = Jogo();
        var previa = FibaAnalisador.Analisar(jogo, FibaTradutor.Traduzir(jogo), RelacaoIgualAoFeed(jogo), Nomes);

        previa.Problemas.Should().BeEmpty();
        previa.PodeAplicar.Should().BeTrue();
        previa.Times.Single(t => t.Lado == LadoTime.Casa).Should().Match<PreviaTimeFiba>(t => t.PlacarFeed == 50 && t.PlacarReconstruido == 50);
        previa.Times.Single(t => t.Lado == LadoTime.Visitante).Should().Match<PreviaTimeFiba>(t => t.PlacarFeed == 68 && t.PlacarReconstruido == 68);
        previa.Times.SelectMany(t => t.Periodos).Should().OnlyContain(p => p.PlacarFeed == p.PlacarReconstruido);
        previa.Times.SelectMany(t => t.Jogadores).Should().OnlyContain(j => j.Divergencias.Count == 0);
    }

    [Fact]
    public void Tempo_e_o_que_falta_no_periodo_e_a_troca_do_intervalo_vem_com_relogio_cheio()
    {
        var tr = FibaTradutor.Traduzir(Jogo());

        tr.Trocas.Where(t => t.Periodo == Periodo.Segundo).Should().Contain(t => t.TempoRestanteSegundos == 600);
        tr.Eventos.Should().OnlyContain(e => e.TempoRestanteSegundos >= 0 && e.TempoRestanteSegundos <= 600);
    }

    [Fact]
    public void Camisa_que_jogou_e_nao_esta_na_relacao_impede_a_importacao()
    {
        var jogo = Jogo();
        var relacao = RelacaoIgualAoFeed(jogo).Where(r => !(r.Lado == LadoTime.Casa && r.Numero == "19")).ToList();

        var previa = FibaAnalisador.Analisar(jogo, FibaTradutor.Traduzir(jogo), relacao, Nomes);

        previa.PodeAplicar.Should().BeFalse();
        previa.Problemas.Should().Contain(p => p.Contains("camisa 19") && p.Contains("não está relacionada"));
    }

    [Fact]
    public void Titular_divergente_vira_aviso_e_nao_impede_a_importacao()
    {
        var jogo = Jogo();
        var relacao = RelacaoIgualAoFeed(jogo)
            .Select(r => r.Lado == LadoTime.Casa && r.Numero == "11" ? r with { Titular = false } : r).ToList();

        var previa = FibaAnalisador.Analisar(jogo, FibaTradutor.Traduzir(jogo), relacao, Nomes);

        previa.PodeAplicar.Should().BeTrue();
        previa.Problemas.Should().BeEmpty();
        previa.Avisos.Should().Contain(p => p.Contains("camisa 11") && p.Contains("titular") && p.Contains("prevalecem"));
    }

    [Fact]
    public void Inverter_os_lados_troca_casa_e_visitante()
    {
        var jogo = Jogo();
        var previa = FibaAnalisador.Analisar(jogo, FibaTradutor.Traduzir(jogo, inverterLados: true),
            RelacaoIgualAoFeed(jogo, inverter: true), Nomes, inverterLados: true);

        previa.PodeAplicar.Should().BeTrue();
        previa.Times.Single(t => t.Lado == LadoTime.Casa).PlacarFeed.Should().Be(68);
    }

    [Theory]
    [InlineData("2888968", "2888968")]
    [InlineData(" 2888968 ", "2888968")]
    [InlineData("https://fibalivestats.dcd.shared.geniussports.com/u/CBBC/2888968/bs.html", null)]
    [InlineData("../2888968", null)]
    [InlineData("28 88968", null)]
    [InlineData("1234567890123", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void So_aceita_o_codigo_do_jogo_em_digitos(string? entrada, string? esperado) =>
        FibaLiveStatsClient.NormalizarCodigo(entrada).Should().Be(esperado);

    [Theory]
    [InlineData("10:00", 600)]
    [InlineData("00:28", 28)]
    [InlineData("", 0)]
    public void Converte_o_relogio_em_segundos(string gt, int esperado) => FibaTradutor.Segundos(gt).Should().Be(esperado);
}
