using Xunit;
using FluentAssertions;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.Tests.Domain;

public class ServicoGeracaoJogosTests
{
    private static readonly Guid TemporadaId = Guid.NewGuid();
    private static readonly Guid CategoriaId = Guid.NewGuid();

    private static List<Equipe> Equipes(int n) =>
        Enumerable.Range(1, n).Select(i => new Equipe(CategoriaId, $"Equipe {i:00}", null)).ToList();

    private static EstruturaFase PontosCorridos(int turnos, int? primeiros = null) => new(turnos, null, null, null, null, primeiros, 0);
    private static EstruturaFase Grupos(int turnos, int grupos, DistribuicaoEquipes d) => new(turnos, grupos, d, null, null, null, 0);
    private static EstruturaFase MataMata(int confrontos, int melhorDe) => new(null, null, null, melhorDe, confrontos, null, 0);

    private static Fase NovaFase(int ordem, string nome, TipoFase tipo, EstruturaFase e, Guid? anterior, int equipes) =>
        new(CategoriaId, ordem, nome, tipo, e, anterior, equipes);

    private static TabelaGerada Gerar(IReadOnlyList<Equipe> equipes, IReadOnlyList<Fase> fases, IReadOnlyList<Confronto>? confrontos = null) =>
        ServicoGeracaoJogos.Gerar(TemporadaId, [new CategoriaParaGerar(CategoriaId, "Sub-15", equipes, fases, confrontos ?? [])]);

    [Fact]
    public void PontosCorridos_8_equipes_1_turno_gera_7_rodadas_de_4_jogos()
    {
        var fase = NovaFase(1, "Pontos corridos", TipoFase.PontosCorridos, PontosCorridos(1), null, 8);

        var tabela = Gerar(Equipes(8), [fase]);

        tabela.Jogos.Should().HaveCount(28);
        tabela.Jogos.Select(j => j.Rodada).Distinct().Should().BeEquivalentTo(Enumerable.Range(1, 7));
        tabela.Vagas.Should().HaveCount(8).And.OnlyContain(v => v.EquipeId != null);
        foreach (var rodada in tabela.Jogos.GroupBy(j => j.Rodada))
        {
            rodada.Should().HaveCount(4);
            rodada.SelectMany(j => new[] { j.CasaId, j.VisitanteId }).Distinct().Should().HaveCount(8);
        }
    }

    [Fact]
    public void Cada_dupla_se_enfrenta_uma_vez_por_turno_e_o_segundo_turno_inverte_o_mando()
    {
        var fase = NovaFase(1, "PC", TipoFase.PontosCorridos, PontosCorridos(2), null, 6);

        var tabela = Gerar(Equipes(6), [fase]);

        tabela.Jogos.Should().HaveCount(30);
        var primeiroTurno = tabela.Jogos.Where(j => j.Rodada <= 5).ToList();
        var segundoTurno = tabela.Jogos.Where(j => j.Rodada > 5).ToList();
        foreach (var j in primeiroTurno)
            segundoTurno.Should().Contain(x => x.CasaId == j.VisitanteId && x.VisitanteId == j.CasaId);
    }

    [Fact]
    public void Numero_impar_de_equipes_tem_uma_folga_por_rodada()
    {
        var fase = NovaFase(1, "PC", TipoFase.PontosCorridos, PontosCorridos(1), null, 5);

        var tabela = Gerar(Equipes(5), [fase]);

        tabela.Jogos.Should().HaveCount(10);
        tabela.Jogos.Select(j => j.Rodada).Distinct().Should().HaveCount(5);
    }

    [Fact]
    public void Grupos_serpentina_distribuem_1_4_5_8_e_2_3_6_7()
    {
        var fase = NovaFase(1, "Grupos", TipoFase.Grupos, Grupos(1, 2, DistribuicaoEquipes.Serpentina), null, 8);

        var tabela = Gerar(Equipes(8), [fase]);

        var a = tabela.Grupos.Single(g => g.Ordem == 1);
        var b = tabela.Grupos.Single(g => g.Ordem == 2);
        tabela.Vagas.Where(v => v.GrupoId == a.Id).Select(v => v.Posicao).Should().BeEquivalentTo(new[] { 1, 4, 5, 8 });
        tabela.Vagas.Where(v => v.GrupoId == b.Id).Select(v => v.Posicao).Should().BeEquivalentTo(new[] { 2, 3, 6, 7 });
        tabela.Jogos.Should().HaveCount(12);
        tabela.Jogos.Should().OnlyContain(j => j.GrupoId != null && j.ConfrontoId == null);
    }

    [Fact]
    public void Grupos_alternada_distribuem_1_3_5_7_e_2_4_6_8()
    {
        var fase = NovaFase(1, "Grupos", TipoFase.Grupos, Grupos(1, 2, DistribuicaoEquipes.Alternada), null, 8);

        var tabela = Gerar(Equipes(8), [fase]);

        var a = tabela.Grupos.Single(g => g.Ordem == 1);
        tabela.Vagas.Where(v => v.GrupoId == a.Id).Select(v => v.Posicao).Should().BeEquivalentTo(new[] { 1, 3, 5, 7 });
    }

    [Fact]
    public void MataMata_melhor_de_3_gera_3_jogos_por_confronto_com_o_terceiro_opcional_e_mando_alternado()
    {
        var fase = NovaFase(1, "Quartas", TipoFase.MataMata, MataMata(2, 3), null, 8);
        var equipes = Equipes(4);
        var confrontos = new[]
        {
            new Confronto(fase.Id, 1, null, ReferenciaEquipe.DeEquipe(equipes[0].Id), ReferenciaEquipe.DeEquipe(equipes[1].Id)),
            new Confronto(fase.Id, 2, null, ReferenciaEquipe.DeEquipe(equipes[2].Id), ReferenciaEquipe.DeEquipe(equipes[3].Id))
        };

        var tabela = Gerar(equipes, [fase], confrontos);

        tabela.Jogos.Should().HaveCount(6);
        var serie = tabela.Jogos.Where(j => j.ConfrontoId == confrontos[0].Id).OrderBy(j => j.JogoDaSerie).ToList();
        serie.Select(j => j.Opcional).Should().Equal(false, false, true);
        serie[0].CasaId.Should().Be(serie[2].CasaId);
        serie[1].CasaId.Should().Be(serie[0].VisitanteId);
    }

    [Fact]
    public void MataMata_sem_todos_os_confrontos_definidos_e_recusado_com_o_nome_da_fase()
    {
        var fase = NovaFase(1, "Quartas", TipoFase.MataMata, MataMata(4, 3), null, 8);

        var act = () => Gerar(Equipes(8), [fase]);

        act.Should().Throw<DomainException>().WithMessage("*Quartas*4 confrontos*");
    }

    [Fact]
    public void Campeonato_completo_do_exemplo_gera_64_jogos_numerados_de_1_a_64()
    {
        var pc = NovaFase(1, "Pontos corridos", TipoFase.PontosCorridos, PontosCorridos(1), null, 8);
        var grupos = NovaFase(2, "Fase de grupos", TipoFase.Grupos, Grupos(1, 2, DistribuicaoEquipes.Serpentina), pc.Id, 8);
        var quartas = NovaFase(3, "Quartas", TipoFase.MataMata, MataMata(4, 3), grupos.Id, 8);
        var semi = NovaFase(4, "Semifinal", TipoFase.MataMata, MataMata(2, 3), quartas.Id, 8);
        var final = NovaFase(5, "Final", TipoFase.MataMata, MataMata(2, 3), semi.Id, 8);

        Confronto C(Fase f, int n, ReferenciaEquipe a, ReferenciaEquipe b) => new(f.Id, n, null, a, b);
        var q = new[]
        {
            C(quartas, 1, ReferenciaEquipe.DeColocacao(grupos.Id, 1, 1), ReferenciaEquipe.DeColocacao(grupos.Id, 2, 4)),
            C(quartas, 2, ReferenciaEquipe.DeColocacao(grupos.Id, 1, 2), ReferenciaEquipe.DeColocacao(grupos.Id, 2, 3)),
            C(quartas, 3, ReferenciaEquipe.DeColocacao(grupos.Id, 2, 1), ReferenciaEquipe.DeColocacao(grupos.Id, 1, 4)),
            C(quartas, 4, ReferenciaEquipe.DeColocacao(grupos.Id, 2, 2), ReferenciaEquipe.DeColocacao(grupos.Id, 1, 3))
        };
        var s = new[]
        {
            C(semi, 1, ReferenciaEquipe.DeVencedor(q[0].Id), ReferenciaEquipe.DeVencedor(q[1].Id)),
            C(semi, 2, ReferenciaEquipe.DeVencedor(q[2].Id), ReferenciaEquipe.DeVencedor(q[3].Id))
        };
        var f = new[]
        {
            C(final, 1, ReferenciaEquipe.DeVencedor(s[0].Id), ReferenciaEquipe.DeVencedor(s[1].Id)),
            C(final, 2, ReferenciaEquipe.DePerdedor(s[0].Id), ReferenciaEquipe.DePerdedor(s[1].Id))
        };

        var tabela = Gerar(Equipes(8), [pc, grupos, quartas, semi, final], [.. q, .. s, .. f]);

        // 28 (pontos corridos) + 12 (grupos) + 12 (quartas) + 6 (semi) + 6 (final e 3º/4º)
        tabela.Jogos.Should().HaveCount(64);
        tabela.Jogos.Select(j => j.Numero).OrderBy(n => n).Should().Equal(Enumerable.Range(1, 64));

        // A fase de grupos vem da classificação de "Pontos corridos": vagas sem equipe, origem = colocação.
        var vagasGrupos = tabela.Vagas.Where(v => v.FaseId == grupos.Id).ToList();
        vagasGrupos.Should().HaveCount(8).And.OnlyContain(v => v.EquipeId == null && v.Origem.Tipo == TipoReferencia.Colocacao && v.Origem.FaseId == pc.Id);

        // A numeração segue a ordem das fases.
        tabela.Jogos.Where(j => j.FaseId == pc.Id).Max(j => j.Numero).Should().BeLessThan(tabela.Jogos.Where(j => j.FaseId == grupos.Id).Min(j => j.Numero));
    }

    [Fact]
    public void Fase_seguinte_a_mata_mata_usa_os_vencedores_como_origem()
    {
        var quartas = NovaFase(1, "Quartas", TipoFase.MataMata, MataMata(2, 1), null, 4);
        var pc = NovaFase(2, "Pontos corridos", TipoFase.PontosCorridos, PontosCorridos(1), quartas.Id, 4);
        var equipes = Equipes(4);
        var confrontos = new[]
        {
            new Confronto(quartas.Id, 1, null, ReferenciaEquipe.DeEquipe(equipes[0].Id), ReferenciaEquipe.DeEquipe(equipes[1].Id)),
            new Confronto(quartas.Id, 2, null, ReferenciaEquipe.DeEquipe(equipes[2].Id), ReferenciaEquipe.DeEquipe(equipes[3].Id))
        };

        var tabela = Gerar(equipes, [quartas, pc], confrontos);

        tabela.Vagas.Where(v => v.FaseId == pc.Id).Should().HaveCount(2)
            .And.OnlyContain(v => v.Origem.Tipo == TipoReferencia.Vencedor);
        tabela.Jogos.Count(j => j.FaseId == pc.Id).Should().Be(1);
    }

    [Fact]
    public void Primeira_fase_sem_equipes_suficientes_e_recusada()
    {
        var fase = NovaFase(1, "PC", TipoFase.PontosCorridos, PontosCorridos(1), null, 0);

        var act = () => Gerar(Equipes(1), [fase]);

        act.Should().Throw<DomainException>().WithMessage("*ao menos 2 equipes*");
    }
}
