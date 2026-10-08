using Xunit;
using FluentAssertions;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.Tests.Domain;

public class SorteioEDistribuicaoManualTests
{
    private static readonly Guid TemporadaId = Guid.NewGuid();
    private static readonly Guid CategoriaId = Guid.NewGuid();

    private static List<Equipe> Equipes(int n) =>
        Enumerable.Range(1, n).Select(i => new Equipe(CategoriaId, $"Equipe {i:00}", null)).ToList();

    private static Fase NovaFase(int ordem, string nome, TipoFase tipo, EstruturaFase e, Guid? anterior, int equipes) =>
        new(CategoriaId, ordem, nome, tipo, e, anterior, equipes);

    private static EstruturaFase PontosCorridos() => new(1, null, null, null, null, null, 0);
    private static EstruturaFase GruposManual(int grupos) => new(1, grupos, DistribuicaoEquipes.Manual, null, null, null, 0);

    private static TabelaGerada Gerar(IReadOnlyList<Equipe> equipes, IReadOnlyList<Fase> fases) =>
        ServicoGeracaoJogos.Gerar(TemporadaId, [new CategoriaParaGerar(CategoriaId, "Sub-15", equipes, fases, [])]);

    private static void Concluir(Jogo j, bool casaVence)
    {
        j.IniciarPelaSumula(Guid.NewGuid());
        j.EncerrarPelaSumula(casaVence ? 60 : 50, casaVence ? 50 : 60);
    }

    private static Jogo Entre(TabelaGerada t, Guid fase, Guid a, Guid b) =>
        t.Jogos.Single(j => j.FaseId == fase && ((j.Casa.EquipeId == a && j.Visitante.EquipeId == b) || (j.Casa.EquipeId == b && j.Visitante.EquipeId == a)));

    // ---------- distribuição manual ----------

    [Fact]
    public void Distribuicao_manual_coloca_cada_equipe_no_grupo_escolhido()
    {
        var e = Equipes(8);
        var fase = NovaFase(1, "Grupos", TipoFase.Grupos, GruposManual(2), null, 8);
        fase.DefinirDistribuicaoManual([1, 1, 1, 1, 2, 2, 2, 2]);
        fase.DefinirDistribuicaoManual([1, 2, 2, 1, 1, 2, 2, 1]);

        var t = Gerar(e, [fase]);

        var grupoA = t.Grupos.Single(g => g.Ordem == 1).Id;
        var ordenadas = e.OrderBy(x => x.Nome).ToList();
        t.Vagas.Where(v => v.GrupoId == grupoA).Select(v => v.EquipeId).Should()
            .BeEquivalentTo(new Guid?[] { ordenadas[0].Id, ordenadas[3].Id, ordenadas[4].Id, ordenadas[7].Id });
    }

    [Fact]
    public void Sem_escolha_manual_a_distribuicao_continua_em_blocos()
    {
        var fase = NovaFase(1, "Grupos", TipoFase.Grupos, GruposManual(2), null, 8);

        var t = Gerar(Equipes(8), [fase]);

        t.Vagas.Where(v => v.GrupoId == t.Grupos.Single(g => g.Ordem == 1).Id).Select(v => v.Posicao).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void Distribuicao_manual_com_numero_de_vagas_diferente_e_recusada_na_geracao()
    {
        var fase = NovaFase(1, "Grupos", TipoFase.Grupos, GruposManual(2), null, 8);
        fase.DefinirDistribuicaoManual([1, 1, 2, 2]);

        var act = () => Gerar(Equipes(8), [fase]);

        act.Should().Throw<DomainException>().WithMessage("*distribuição manual*");
    }

    [Fact]
    public void Distribuicao_manual_exige_ao_menos_2_equipes_por_grupo_e_grupos_validos()
    {
        var fase = NovaFase(1, "Grupos", TipoFase.Grupos, GruposManual(2), null, 8);

        var sozinho = () => fase.DefinirDistribuicaoManual([1, 2, 2, 2, 2, 2, 2, 2]);
        var invalido = () => fase.DefinirDistribuicaoManual([1, 1, 2, 2, 3, 3, 1, 2]);

        sozinho.Should().Throw<DomainException>().WithMessage("*grupo A precisa de ao menos 2*");
        invalido.Should().Throw<DomainException>().WithMessage("*entre A e B*");
    }

    [Fact]
    public void Distribuicao_manual_so_vale_em_fase_de_grupos_com_distribuicao_manual()
    {
        var pc = NovaFase(1, "PC", TipoFase.PontosCorridos, PontosCorridos(), null, 8);
        var serpentina = NovaFase(1, "Grupos", TipoFase.Grupos, new EstruturaFase(1, 2, DistribuicaoEquipes.Serpentina, null, null, null, 0), null, 8);

        ((Action)(() => pc.DefinirDistribuicaoManual([1, 2]))).Should().Throw<DomainException>();
        ((Action)(() => serpentina.DefinirDistribuicaoManual([1, 1, 2, 2]))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Mudar_a_estrutura_da_fase_descarta_a_escolha_manual_mas_mudar_o_nome_nao()
    {
        var fase = NovaFase(1, "Grupos", TipoFase.Grupos, GruposManual(2), null, 8);
        fase.DefinirDistribuicaoManual([1, 2, 2, 1, 1, 2, 2, 1]);

        fase.Editar(false, "Fase de grupos", TipoFase.Grupos, GruposManual(2), null, 8);
        fase.DistribuicaoManual.Should().NotBeNull();

        fase.Editar(false, "Fase de grupos", TipoFase.Grupos, new EstruturaFase(1, 2, DistribuicaoEquipes.Manual, null, null, 2, 0), null, 8);
        fase.DistribuicaoManual.Should().BeNull();
    }

    // ---------- sorteio de desempate ----------

    private static readonly Participante A = new(Guid.NewGuid(), "Águias");
    private static readonly Participante B = new(Guid.NewGuid(), "Bravos");
    private static readonly Participante C = new(Guid.NewGuid(), "Corvos");

    private static ResultadoJogo J(Participante casa, Participante vis) => new(casa.Id, vis.Id, 60, 50, false);

    [Fact]
    public void Sorteio_registrado_define_a_ordem_do_empate_e_marca_como_definido()
    {
        // A vence B, B vence C, C vence A com a mesma margem: empate triplo sem critério.
        var jogos = new[] { J(A, B), J(B, C), J(C, A) };

        var semSorteio = ServicoClassificacao.Classificar([A, B, C], jogos);
        var comSorteio = ServicoClassificacao.Classificar([A, B, C], jogos, [C.Id, A.Id, B.Id]);

        semSorteio.Should().OnlyContain(l => l.EmpatePorSorteio && !l.SorteioDefinido);
        comSorteio.Select(l => l.Participante).Should().Equal(C, A, B);
        comSorteio.Should().OnlyContain(l => l.EmpatePorSorteio && l.SorteioDefinido);
    }

    [Fact]
    public void Sorteio_que_nao_cobre_todos_os_empatados_e_ignorado()
    {
        var jogos = new[] { J(A, B), J(B, C), J(C, A) };

        var r = ServicoClassificacao.Classificar([A, B, C], jogos, [C.Id, A.Id]);

        r.Should().OnlyContain(l => !l.SorteioDefinido);
    }

    [Fact]
    public void Avanco_resolve_as_vagas_seguintes_com_o_sorteio_registrado()
    {
        var pc = NovaFase(1, "PC", TipoFase.PontosCorridos, PontosCorridos(), null, 3);
        var proxima = NovaFase(2, "Segunda", TipoFase.PontosCorridos, PontosCorridos(), pc.Id, 3);
        var e = Equipes(3);
        var t = Gerar(e, [pc, proxima]);
        Concluir(Entre(t, pc.Id, e[0].Id, e[1].Id), Entre(t, pc.Id, e[0].Id, e[1].Id).Casa.EquipeId == e[0].Id);
        Concluir(Entre(t, pc.Id, e[1].Id, e[2].Id), Entre(t, pc.Id, e[1].Id, e[2].Id).Casa.EquipeId == e[1].Id);
        Concluir(Entre(t, pc.Id, e[2].Id, e[0].Id), Entre(t, pc.Id, e[2].Id, e[0].Id).Casa.EquipeId == e[2].Id);

        var pendente = ServicoAvanco.Avancar([pc, proxima], t.Grupos, t.Vagas, [], t.Jogos);
        pendente.VagasResolvidas.Should().BeEmpty();

        var vagasPc = t.Vagas.Where(v => v.FaseId == pc.Id).ToList();
        var ordem = new[] { e[2], e[0], e[1] }.Select(eq => vagasPc.Single(v => v.EquipeId == eq.Id).Id).ToList();
        var sorteio = new SorteioDeDesempate(pc.Id, null, ordem);

        var r = ServicoAvanco.Avancar([pc, proxima], t.Grupos, t.Vagas, [], t.Jogos, null, [sorteio]);

        r.VagasResolvidas.Should().HaveCount(3);
        t.Vagas.Where(v => v.FaseId == proxima.Id).OrderBy(v => v.Posicao).Select(v => v.EquipeId)
            .Should().Equal(e[2].Id, e[0].Id, e[1].Id);
    }

    [Fact]
    public void Sorteio_precisa_de_ao_menos_duas_equipes_sem_repeticao()
    {
        var a = Guid.NewGuid();

        ((Action)(() => new SorteioDeDesempate(Guid.NewGuid(), null, [a]))).Should().Throw<DomainException>();
        ((Action)(() => new SorteioDeDesempate(Guid.NewGuid(), null, [a, a]))).Should().Throw<DomainException>();
    }
}
