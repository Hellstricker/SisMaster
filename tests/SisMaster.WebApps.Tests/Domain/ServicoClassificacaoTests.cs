using Xunit;
using FluentAssertions;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.Tests.Domain;

public class ServicoClassificacaoTests
{
    private static readonly Participante A = new(Guid.NewGuid(), "Águias");
    private static readonly Participante B = new(Guid.NewGuid(), "Bravos");
    private static readonly Participante C = new(Guid.NewGuid(), "Corvos");
    private static readonly Participante D = new(Guid.NewGuid(), "Dragões");

    private static ResultadoJogo J(Participante casa, int pc, Participante vis, int pv, bool wo = false, decimal bc = 0, decimal bv = 0) =>
        new(casa.Id, vis.Id, pc, pv, wo, bc, bv);

    private static LinhaClassificacao Linha(IReadOnlyList<LinhaClassificacao> t, Participante p) => t.Single(l => l.Participante.Id == p.Id);

    [Fact]
    public void Vitoria_vale_2_derrota_1_e_wo_0()
    {
        var t = ServicoClassificacao.Classificar([A, B, C], [J(A, 50, B, 40), J(A, 20, C, 0, wo: true)]);

        Linha(t, A).Should().Match<LinhaClassificacao>(l => l.Vitorias == 2 && l.Subtotal == 4);
        Linha(t, B).Should().Match<LinhaClassificacao>(l => l.Derrotas == 1 && l.PontosPorDerrotas == 1 && l.Subtotal == 1);
        Linha(t, C).Should().Match<LinhaClassificacao>(l => l.DerrotasWO == 1 && l.PontosPorWO == 0 && l.Subtotal == 0);
    }

    [Fact]
    public void Total_e_subtotal_mais_somatorio_da_bonificacao()
    {
        var t = ServicoClassificacao.Classificar([A, B], [J(A, 60, B, 50, bc: 3, bv: 2), J(B, 55, A, 54, bc: 1, bv: 4)]);

        var a = Linha(t, A);
        a.Subtotal.Should().Be(3);          // 1 vitória (2) + 1 derrota (1)
        a.Bonificacao.Should().Be(7);       // 3 + 4
        a.Total.Should().Be(10);
        Linha(t, B).Total.Should().Be(3 + 3); // 3 de resultado + (2 + 1) de bonificação
    }

    [Fact]
    public void Bonificacao_pode_alterar_a_ordem()
    {
        var t = ServicoClassificacao.Classificar([A, B], [J(A, 50, B, 40, bc: 0, bv: 5)]);

        t[0].Participante.Should().Be(B);   // B: 1 + 5 = 6 contra 2 de A
    }

    [Fact]
    public void Jogos_de_equipes_fora_da_tabela_sao_ignorados()
    {
        var t = ServicoClassificacao.Classificar([A, B], [J(A, 50, C, 40)]);

        t.Should().OnlyContain(l => l.Jogos == 0);
    }

    [Fact]
    public void Empate_em_pontos_resolve_pelo_confronto_direto()
    {
        // A, B e C: 1 vitória e 1 derrota cada, mas entre os três A vence B, B vence C e C vence A → resolve por saldo no confronto direto
        var t = ServicoClassificacao.Classificar([A, B, C], [J(A, 60, B, 50), J(B, 52, C, 50), J(C, 70, A, 62)]);

        // Saldos: A = +10 -8 = +2; B = -10 +2 = -8; C = -2 +8 = +6
        t.Select(l => l.Participante).Should().Equal(C, A, B);
        t.Should().OnlyContain(l => !l.EmpatePorSorteio);
    }

    [Fact]
    public void Empate_triplo_circular_resolve_pelo_saldo_entre_os_empatados()
    {
        // A vence B por 1, B vence C por 60, C vence A por 40: todos com 3 pontos e saldo entre si A -39, B +59, C -20
        var t = ServicoClassificacao.Classificar([A, B, C], [J(A, 51, B, 50), J(B, 90, C, 30), J(C, 80, A, 40)]);

        t.Should().OnlyContain(l => l.Total == 3);
        t.Select(l => l.Participante).Should().Equal(B, C, A);
    }

    [Fact]
    public void Empate_total_vira_sorteio_em_ordem_alfabetica()
    {
        var t = ServicoClassificacao.Classificar([B, A], [J(A, 50, B, 50)]);
        // placar igual não ocorre no basquete, mas o serviço não pode quebrar: casa "vence" por convenção
        t.Should().HaveCount(2);

        var iguais = ServicoClassificacao.Classificar([A, B, C, D], []);
        iguais.Select(l => l.Participante).Should().Equal(A, B, C, D);
        iguais.Should().OnlyContain(l => l.EmpatePorSorteio);
    }

    [Fact]
    public void Sequencia_guarda_V_e_D_na_ordem_dos_jogos()
    {
        var t = ServicoClassificacao.Classificar([A, B], [J(A, 50, B, 40), J(A, 30, B, 40), J(B, 10, A, 20)]);

        new string(Linha(t, A).Sequencia.ToArray()).Should().Be("VDV");
    }

    [Fact]
    public void Melhores_ordena_por_pontos_saldo_e_pontos_feitos()
    {
        var t1 = ServicoClassificacao.Classificar([A, B], [J(A, 50, B, 40)]);
        var t2 = ServicoClassificacao.Classificar([C, D], [J(C, 80, D, 40)]);

        var ranking = ServicoClassificacao.OrdenarMelhores([Linha(t1, B), Linha(t2, D)]);

        ranking.Select(l => l.Participante).Should().Equal(B, D); // mesmos pontos; saldo -10 contra -40
    }

    [Fact]
    public void Serie_melhor_de_3_e_decidida_com_2_vitorias()
    {
        ServicoSerie.VitoriasNecessarias(3).Should().Be(2);
        ServicoSerie.VitoriasNecessarias(1).Should().Be(1);
        ServicoSerie.VitoriasNecessarias(5).Should().Be(3);

        ServicoSerie.Vencedor(A.Id, B.Id, [J(A, 50, B, 40)], 3).Should().BeNull();
        ServicoSerie.Vencedor(A.Id, B.Id, [J(A, 50, B, 40), J(B, 30, A, 60)], 3).Should().Be(A.Id);
    }

    [Fact]
    public void Serie_conta_vitorias_mesmo_com_mando_alternado()
    {
        var jogos = new[] { J(A, 50, B, 40), J(B, 55, A, 50), J(A, 45, B, 44) };

        ServicoSerie.Placar(A.Id, B.Id, jogos).Should().Be((2, 1));
        ServicoSerie.Vencedor(A.Id, B.Id, jogos, 3).Should().Be(A.Id);
    }
}
