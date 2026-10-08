using Xunit;
using FluentAssertions;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.Tests.Domain;

public class ServicoRodizioTests
{
    private static readonly Guid[] J = Enumerable.Range(1, 9).Select(_ => Guid.NewGuid()).ToArray();

    /// <summary>Nove relacionados; os cinco primeiros são titulares.</summary>
    private static List<(Guid Id, bool Titular)> Elenco() => J.Select((id, i) => (id, i < 5)).ToList();

    private static EstadoPeriodo[] Estados(IReadOnlyList<RodizioDoJogador> r, int jogador) => r.Single(x => x.JogadorId == J[jogador]).Periodos.ToArray();

    [Fact]
    public void Sem_substituicoes_titulares_ficam_completos_e_reservas_fora()
    {
        var r = ServicoRodizio.Calcular(Elenco(), [], 4);

        Estados(r, 0).Should().OnlyContain(e => e == EstadoPeriodo.Completo);
        Estados(r, 8).Should().OnlyContain(e => e == EstadoPeriodo.Fora);
        r[0].Cumpre(1, 1).Should().BeFalse();
    }

    [Fact]
    public void Troca_no_intervalo_vale_como_inicio_do_periodo_seguinte()
    {
        // Titular 0 sai e reserva 5 entra no início do 2º período.
        var r = ServicoRodizio.Calcular(Elenco(), [new TrocaDeQuadra(J[0], J[5], 2, true)], 2);

        Estados(r, 0).Should().Equal(EstadoPeriodo.Completo, EstadoPeriodo.Fora);
        Estados(r, 5).Should().Equal(EstadoPeriodo.Fora, EstadoPeriodo.Completo);
        r.Single(x => x.JogadorId == J[0]).Cumpre(1, 1).Should().BeTrue();
        r.Single(x => x.JogadorId == J[5]).Cumpre(1, 1).Should().BeTrue();
    }

    [Fact]
    public void Troca_no_meio_do_periodo_deixa_os_dois_parciais()
    {
        var r = ServicoRodizio.Calcular(Elenco(), [new TrocaDeQuadra(J[0], J[5], 1, false)], 2);

        Estados(r, 0).Should().Equal(EstadoPeriodo.Parcial, EstadoPeriodo.Fora);
        Estados(r, 5).Should().Equal(EstadoPeriodo.Parcial, EstadoPeriodo.Completo);
    }

    [Fact]
    public void Entrar_e_voltar_para_o_banco_no_mesmo_periodo_e_parcial()
    {
        var trocas = new[] { new TrocaDeQuadra(J[0], J[5], 1, false), new TrocaDeQuadra(J[5], J[0], 1, false) };

        var r = ServicoRodizio.Calcular(Elenco(), trocas, 1);

        Estados(r, 0).Should().Equal(EstadoPeriodo.Parcial);
        Estados(r, 5).Should().Equal(EstadoPeriodo.Parcial);
    }

    [Fact]
    public void So_conta_os_periodos_encerrados_e_ate_o_quarto()
    {
        Calcular(0).Should().OnlyContain(j => j.Periodos.Count == 0);
        Calcular(2).Should().OnlyContain(j => j.Periodos.Count == 2);
        Calcular(9).Should().OnlyContain(j => j.Periodos.Count == 4);

        static IReadOnlyList<RodizioDoJogador> Calcular(int p) => ServicoRodizio.Calcular(Elenco(), [], p);
    }

    [Fact]
    public void Bonificacao_conta_quem_cumpriu_vezes_o_valor_por_atleta()
    {
        // 9 relacionados; só os jogadores 0 (sai no 2º) e 5 (entra no 2º) cumprem 1 em quadra + 1 fora.
        var r = ServicoRodizio.Calcular(Elenco(), [new TrocaDeQuadra(J[0], J[5], 2, true)], 2);

        ServicoRodizio.Bonificacao(r, 1, 1, 1.5m).Should().Be(3m);
        ServicoRodizio.Bonificacao(r, 1, 1, 0m).Should().Be(0m);
    }

    [Fact]
    public void Menos_de_7_relacionados_nao_ganha_bonificacao()
    {
        var seis = J.Take(6).Select((id, i) => (id, i < 5)).ToList();
        var r = ServicoRodizio.Calcular(seis, [new TrocaDeQuadra(J[0], J[5], 2, true)], 2);

        r.Single(x => x.JogadorId == J[0]).Cumpre(1, 1).Should().BeTrue();
        ServicoRodizio.Bonificacao(r, 1, 1, 1m).Should().Be(0m);
    }
}

