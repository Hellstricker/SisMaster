using Xunit;
using FluentAssertions;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.Tests.Domain;

public class ServicoAvancoTests
{
    private static readonly Guid TemporadaId = Guid.NewGuid();
    private static readonly Guid CategoriaId = Guid.NewGuid();

    private static List<Equipe> Equipes(int n) =>
        Enumerable.Range(1, n).Select(i => new Equipe(CategoriaId, $"Equipe {i:00}", null)).ToList();

    private static Fase NovaFase(int ordem, string nome, TipoFase tipo, EstruturaFase e, Guid? anterior, int equipes) =>
        new(CategoriaId, ordem, nome, tipo, e, anterior, equipes);

    private static EstruturaFase PontosCorridos() => new(1, null, null, null, null, null, 0);
    private static EstruturaFase Grupos(int grupos) => new(1, grupos, DistribuicaoEquipes.Serpentina, null, null, null, 0);
    private static EstruturaFase MataMata(int confrontos, int melhorDe) => new(null, null, null, melhorDe, confrontos, null, 0);

    private static TabelaGerada Gerar(IReadOnlyList<Equipe> equipes, IReadOnlyList<Fase> fases, IReadOnlyList<Confronto>? confrontos = null) =>
        ServicoGeracaoJogos.Gerar(TemporadaId, [new CategoriaParaGerar(CategoriaId, "Sub-15", equipes, fases, confrontos ?? [])]);

    private static ResultadoAvanco Avancar(IReadOnlyList<Fase> fases, TabelaGerada t, IReadOnlyList<Confronto>? confrontos = null) =>
        ServicoAvanco.Avancar(fases, t.Grupos, t.Vagas, confrontos ?? [], t.Jogos);

    /// <summary>Encerra o jogo dando a vitória à equipe indicada.</summary>
    private static void Concluir(TabelaGerada t, Jogo j, Guid vencedorEquipeId, int margem = 10)
    {
        var casaVence = j.Casa.EquipeId == vencedorEquipeId;
        j.IniciarPelaSumula(Guid.NewGuid());
        j.EncerrarPelaSumula(casaVence ? 50 + margem : 50, casaVence ? 50 : 50 + margem);
    }

    private static Jogo Entre(TabelaGerada t, Guid fase, Guid equipeA, Guid equipeB) =>
        t.Jogos.Single(j => j.FaseId == fase && ((j.Casa.EquipeId == equipeA && j.Visitante.EquipeId == equipeB) || (j.Casa.EquipeId == equipeB && j.Visitante.EquipeId == equipeA)));

    [Fact]
    public void Classificacao_da_fase_resolve_as_vagas_da_fase_seguinte_quando_a_tabela_termina()
    {
        var pc = NovaFase(1, "Pontos corridos", TipoFase.PontosCorridos, PontosCorridos(), null, 4);
        var grupos = NovaFase(2, "Grupos", TipoFase.Grupos, Grupos(2), pc.Id, 4);
        var equipes = Equipes(4);
        var t = Gerar(equipes, [pc, grupos]);

        // Equipe 01 vence todas, depois a 02, depois a 03: ranking 1, 2, 3, 4.
        foreach (var j in t.Jogos.Where(j => j.FaseId == pc.Id))
        {
            var casa = equipes.First(e => e.Id == j.Casa.EquipeId);
            var vis = equipes.First(e => e.Id == j.Visitante.EquipeId);
            Concluir(t, j, equipes.IndexOf(casa) < equipes.IndexOf(vis) ? casa.Id : vis.Id);
        }

        var r = Avancar([pc, grupos], t);

        r.VagasResolvidas.Should().HaveCount(4);
        var vagas = t.Vagas.Where(v => v.FaseId == grupos.Id).ToList();
        vagas.Should().OnlyContain(v => v.EquipeId != null);
        // Serpentina com 2 grupos: A = 1º e 4º; B = 2º e 3º.
        var ordemGrupoA = t.Grupos.Single(g => g.FaseId == grupos.Id && g.Ordem == 1).Id;
        vagas.Where(v => v.GrupoId == ordemGrupoA).Select(v => v.EquipeId).Should().BeEquivalentTo(new Guid?[] { equipes[0].Id, equipes[3].Id });
    }

    [Fact]
    public void Tabela_incompleta_nao_resolve_nada()
    {
        var pc = NovaFase(1, "Pontos corridos", TipoFase.PontosCorridos, PontosCorridos(), null, 4);
        var grupos = NovaFase(2, "Grupos", TipoFase.Grupos, Grupos(2), pc.Id, 4);
        var equipes = Equipes(4);
        var t = Gerar(equipes, [pc, grupos]);
        var primeiro = t.Jogos.First(j => j.FaseId == pc.Id);
        Concluir(t, primeiro, primeiro.Casa.EquipeId!.Value);

        var r = Avancar([pc, grupos], t);

        r.VagasResolvidas.Should().BeEmpty();
        t.Vagas.Where(v => v.FaseId == grupos.Id).Should().OnlyContain(v => v.EquipeId == null);
    }

    [Fact]
    public void Empate_sem_criterio_nao_e_resolvido_sozinho()
    {
        var pc = NovaFase(1, "Pontos corridos", TipoFase.PontosCorridos, PontosCorridos(), null, 3);
        var proxima = NovaFase(2, "Segunda", TipoFase.PontosCorridos, PontosCorridos(), pc.Id, 3);
        var e = Equipes(3);
        var t = Gerar(e, [pc, proxima]);
        // A vence B, B vence C, C vence A, todos pela mesma margem: pontos, saldo e pontos feitos iguais.
        Concluir(t, Entre(t, pc.Id, e[0].Id, e[1].Id), e[0].Id);
        Concluir(t, Entre(t, pc.Id, e[1].Id, e[2].Id), e[1].Id);
        Concluir(t, Entre(t, pc.Id, e[2].Id, e[0].Id), e[2].Id);

        var r = Avancar([pc, proxima], t);

        r.VagasResolvidas.Should().BeEmpty();
        r.VagasAguardandoSorteio.Should().NotBeEmpty();
    }

    [Fact]
    public void Serie_decidida_dispensa_o_jogo_se_necessario_e_resolve_o_vencedor_na_fase_seguinte()
    {
        var quartas = NovaFase(1, "Quartas", TipoFase.MataMata, MataMata(2, 3), null, 4);
        var final = NovaFase(2, "Final", TipoFase.PontosCorridos, PontosCorridos(), quartas.Id, 4);
        var e = Equipes(4);
        var confrontos = new[]
        {
            new Confronto(quartas.Id, 1, null, ReferenciaEquipe.DeEquipe(e[0].Id), ReferenciaEquipe.DeEquipe(e[1].Id)),
            new Confronto(quartas.Id, 2, null, ReferenciaEquipe.DeEquipe(e[2].Id), ReferenciaEquipe.DeEquipe(e[3].Id))
        };
        var t = Gerar(e, [quartas, final], confrontos);
        var serie1 = t.Jogos.Where(j => j.ConfrontoId == confrontos[0].Id).OrderBy(j => j.JogoDaSerie).ToList();

        // Uma vitória só não decide a série.
        Concluir(t, serie1[0], e[0].Id);
        Avancar([quartas, final], t, confrontos).Dispensados.Should().BeEmpty();

        Concluir(t, serie1[1], e[0].Id);
        var r = Avancar([quartas, final], t, confrontos);

        r.Dispensados.Should().ContainSingle().Which.Should().Be(serie1[2]);
        serie1[2].Status.Should().Be(StatusJogo.Dispensado);
        // A fase seguinte ganha só o vencedor do confronto 1; o confronto 2 segue em aberto.
        var vagasFinal = t.Vagas.Where(v => v.FaseId == final.Id).ToList();
        vagasFinal.Where(v => v.EquipeId != null).Select(v => v.EquipeId).Should().Equal(e[0].Id);
        t.Jogos.Where(j => j.ConfrontoId == confrontos[1].Id).Should().OnlyContain(j => j.Status == StatusJogo.Agendado);
    }

    [Fact]
    public void Avancar_de_novo_nao_muda_mais_nada()
    {
        var quartas = NovaFase(1, "Quartas", TipoFase.MataMata, MataMata(2, 1), null, 4);
        var final = NovaFase(2, "Final", TipoFase.PontosCorridos, PontosCorridos(), quartas.Id, 4);
        var e = Equipes(4);
        var confrontos = new[]
        {
            new Confronto(quartas.Id, 1, null, ReferenciaEquipe.DeEquipe(e[0].Id), ReferenciaEquipe.DeEquipe(e[1].Id)),
            new Confronto(quartas.Id, 2, null, ReferenciaEquipe.DeEquipe(e[2].Id), ReferenciaEquipe.DeEquipe(e[3].Id))
        };
        var t = Gerar(e, [quartas, final], confrontos);
        foreach (var j in t.Jogos.Where(j => j.FaseId == quartas.Id)) Concluir(t, j, j.Casa.EquipeId!.Value);

        Avancar([quartas, final], t, confrontos).VagasResolvidas.Should().HaveCount(2);
        var segundo = Avancar([quartas, final], t, confrontos);

        segundo.VagasResolvidas.Should().BeEmpty();
        segundo.Dispensados.Should().BeEmpty();
    }
    [Fact]
    public void Fase_depois_de_grupos_recebe_os_classificados_ordenados_por_posicao_pontos_e_saldo()
    {
        var grupos = NovaFase(1, "Grupos", TipoFase.Grupos, new EstruturaFase(1, 2, DistribuicaoEquipes.Serpentina, null, null, 1, 0), null, 4);
        var final = NovaFase(2, "Final", TipoFase.PontosCorridos, PontosCorridos(), grupos.Id, 4);
        var e = Equipes(4);
        var t = Gerar(e, [grupos, final]);

        // Serpentina com 4 equipes: grupo A = Equipe 01 e 04; grupo B = Equipe 02 e 03.
        var jogoA = Entre(t, grupos.Id, e[0].Id, e[3].Id);
        var jogoB = Entre(t, grupos.Id, e[1].Id, e[2].Id);
        ConcluirPlacar(jogoA, e[0].Id, 70, 50);   // saldo +20
        ConcluirPlacar(jogoB, e[2].Id, 60, 50);   // saldo +10

        var r = Avancar([grupos, final], t);

        r.VagasResolvidas.Should().HaveCount(2);
        t.Vagas.Where(v => v.FaseId == final.Id).OrderBy(v => v.Posicao).Select(v => v.EquipeId).Should().Equal(e[0].Id, e[2].Id);
    }

    private static void ConcluirPlacar(Jogo j, Guid vencedorEquipeId, int pontosVencedor, int pontosPerdedor)
    {
        var casaVence = j.Casa.EquipeId == vencedorEquipeId;
        j.IniciarPelaSumula(Guid.NewGuid());
        j.EncerrarPelaSumula(casaVence ? pontosVencedor : pontosPerdedor, casaVence ? pontosPerdedor : pontosVencedor);
    }
}
