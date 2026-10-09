using FluentAssertions;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using Xunit;

namespace SisMaster.WebApps.Tests.Domain;

public class ConferenciaDeAgendaTests
{
    private static readonly Guid Temporada = Guid.NewGuid();
    private static readonly Guid LocalA = Guid.NewGuid();
    private static readonly Guid LocalB = Guid.NewGuid();
    private static readonly Guid Aguias = Guid.NewGuid(), Ursos = Guid.NewGuid(), Lobos = Guid.NewGuid(), Tigres = Guid.NewGuid();
    private static readonly DateOnly Dia = new(2026, 11, 8);
    private static readonly PessoaDoElenco Ozias = new(Guid.NewGuid(), "Ozias Costa");

    private static JogoNaAgenda Jogo(int numero, Guid? casa, Guid? visitante, string? hora, Guid? local = null, DateOnly? data = null) =>
        new(Guid.NewGuid(), Temporada, numero, casa, visitante, hora is null ? null : data ?? Dia, hora is null ? null : TimeOnly.Parse(hora),
            local, local == LocalA ? "Ginásio A" : local == LocalB ? "Ginásio B" : null);

    private static ElencosDaTemporada Elencos(params (Guid Equipe, string Nome, PessoaDoElenco[] Pessoas)[] equipes) =>
        new(equipes.ToDictionary(e => e.Equipe, e => e.Nome),
            equipes.ToDictionary(e => e.Equipe, e => (IReadOnlyList<PessoaDoElenco>)e.Pessoas));

    private static readonly ElencosDaTemporada Padrao = Elencos(
        (Aguias, "Águias", [Ozias]), (Ursos, "Ursos", []), (Lobos, "Lobos", [Ozias]), (Tigres, "Tigres", []));

    private static string Descrever(JogoNaAgenda j) => $"#{j.Numero}";

    private static ResultadoDaConferencia Conferir(JogoNaAgenda[] propostos, params JogoNaAgenda[] existentes) =>
        ConferenciaDeAgenda.Conferir(propostos, existentes, Padrao, Descrever);

    [Fact]
    public void Mesmo_local_com_menos_de_90_minutos_bloqueia()
    {
        var existente = Jogo(1, Aguias, Ursos, "10:00", LocalA);
        var novo = Jogo(2, Lobos, Tigres, "11:29", LocalA);

        var r = Conferir([novo], existente);

        r.Ok.Should().BeFalse();
        r.Bloqueios.Should().ContainSingle().Which.Should().Contain("Local ocupado").And.Contain("#1").And.Contain("Ginásio A");
    }

    [Fact]
    public void Exatamente_90_minutos_no_mesmo_local_nao_conflita()
    {
        var r = Conferir([Jogo(2, Lobos, Tigres, "11:30", LocalA)], Jogo(1, Aguias, Ursos, "10:00", LocalA));

        r.Bloqueios.Should().BeEmpty();
    }

    [Fact]
    public void Locais_diferentes_ou_datas_diferentes_nao_conflitam()
    {
        var existente = Jogo(1, Aguias, Ursos, "10:00", LocalA);

        Conferir([Jogo(2, Lobos, Tigres, "10:00", LocalB)], existente).Bloqueios.Should().BeEmpty();
        Conferir([Jogo(3, Lobos, Tigres, "10:00", LocalA, Dia.AddDays(1))], existente).Bloqueios.Should().BeEmpty();
    }

    [Fact]
    public void Sem_local_definido_nao_ha_conflito_de_local()
    {
        var r = Conferir([Jogo(2, Lobos, Tigres, "10:00", null)], Jogo(1, Aguias, Ursos, "10:00", null));

        r.Bloqueios.Should().BeEmpty();
    }

    [Fact]
    public void A_mesma_equipe_em_dois_jogos_proximos_bloqueia_mesmo_em_locais_diferentes()
    {
        var existente = Jogo(1, Aguias, Ursos, "10:00", LocalA);
        var novo = Jogo(2, Ursos, Tigres, "10:45", LocalB);

        var r = Conferir([novo], existente);

        r.Bloqueios.Should().ContainSingle().Which.Should().Contain("Ursos já joga").And.Contain("#1");
    }

    [Fact]
    public void Equipe_ainda_indefinida_nao_entra_na_conta()
    {
        var existente = Jogo(1, null, null, "10:00", LocalA);
        var novo = Jogo(2, null, Tigres, "10:00", LocalB);

        Conferir([novo], existente).Bloqueios.Should().BeEmpty();
    }

    [Fact]
    public void O_mesmo_atleta_em_duas_equipes_que_jogam_sobrepostas_so_avisa()
    {
        var existente = Jogo(1, Aguias, Ursos, "10:00", LocalA);
        var novo = Jogo(2, Lobos, Tigres, "10:30", LocalB);

        var r = Conferir([novo], existente);

        r.Ok.Should().BeTrue("aviso não bloqueia");
        r.Bloqueios.Should().BeEmpty();
        r.Avisos.Should().ContainSingle().Which.Should().Contain("Ozias Costa").And.Contain("Águias").And.Contain("Lobos");
    }

    [Fact]
    public void Sem_sobreposicao_nao_ha_aviso_de_atleta()
    {
        var r = Conferir([Jogo(2, Lobos, Tigres, "12:00", LocalB)], Jogo(1, Aguias, Ursos, "10:00", LocalA));

        r.Avisos.Should().BeEmpty();
    }

    [Fact]
    public void Jogos_do_proprio_lote_tambem_sao_conferidos_entre_si()
    {
        var a = Jogo(1, Aguias, Ursos, "10:00", LocalA);
        var b = Jogo(2, Lobos, Tigres, "11:00", LocalA);

        var r = ConferenciaDeAgenda.Conferir([a, b], [], Padrao, Descrever);

        r.Bloqueios.Should().ContainSingle().Which.Should().Contain("Local ocupado");
    }

    [Fact]
    public void Proposto_sem_data_ou_hora_nao_e_conferido()
    {
        var r = Conferir([Jogo(2, Aguias, Ursos, null, LocalA)], Jogo(1, Aguias, Ursos, "10:00", LocalA));

        r.Bloqueios.Should().BeEmpty();
        r.Avisos.Should().BeEmpty();
    }

    [Fact]
    public void O_proprio_jogo_nao_conflita_com_ele_mesmo()
    {
        var jogo = Jogo(1, Aguias, Ursos, "10:00", LocalA);

        ConferenciaDeAgenda.Conferir([jogo], [jogo], Padrao, Descrever).Bloqueios.Should().BeEmpty();
    }
}
