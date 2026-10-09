using FluentAssertions;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using Xunit;

namespace SisMaster.WebApps.Tests.Domain;

/// <summary>Regras de transição de status da temporada (tela G6): iniciar, encerrar, reabrir inscrições, datas.</summary>
public class TemporadaCicloDeVidaTests
{
    private static Temporada Nova() => new(Guid.NewGuid(), 2026, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

    private static Temporada ComInscricoesEncerradas()
    {
        var t = Nova();
        t.EncerrarInscricoes();
        return t;
    }

    private static Temporada ComTabelaGerada()
    {
        var t = ComInscricoesEncerradas();
        t.EncerrarCadastroFases(existeFase: true);
        t.MarcarTabelaJogosGerada();
        return t;
    }

    [Fact]
    public void Iniciar_exige_a_tabela_de_jogos_gerada()
    {
        var semTabela = ComInscricoesEncerradas();

        var iniciar = () => semTabela.Iniciar();

        iniciar.Should().Throw<DomainException>().WithMessage("*tabela de jogos*");
        semTabela.Status.Should().Be(StatusTemporada.InscricoesEncerradas);
    }

    [Fact]
    public void Iniciar_com_a_tabela_gerada_funciona()
    {
        var t = ComTabelaGerada();

        t.Iniciar();

        t.Status.Should().Be(StatusTemporada.EmAndamento);
    }

    [Fact]
    public void Encerrar_so_com_todos_os_jogos_decididos()
    {
        var t = ComTabelaGerada();
        t.Iniciar();

        var comUm = () => t.Encerrar(1);
        var comNove = () => t.Encerrar(9);

        comUm.Should().Throw<DomainException>().WithMessage("Há 1 jogo ainda não encerrado*");
        comNove.Should().Throw<DomainException>().WithMessage("Há 9 jogos ainda não encerrados*");
        t.Status.Should().Be(StatusTemporada.EmAndamento);

        t.Encerrar(0);
        t.Status.Should().Be(StatusTemporada.Encerrada);
    }

    [Fact]
    public void Nao_se_reabre_a_inscricao_depois_de_gerar_as_cobrancas()
    {
        var t = ComInscricoesEncerradas();

        var reabrir = () => t.AlterarInscricoesHabilitadas(true, cobrancasGeradas: true);

        reabrir.Should().Throw<DomainException>().WithMessage("*cobranças geradas*");
        t.InscricoesHabilitadas.Should().BeFalse();
    }

    [Fact]
    public void Reabre_a_inscricao_enquanto_nao_ha_cobranca()
    {
        var t = ComInscricoesEncerradas();

        t.AlterarInscricoesHabilitadas(true, cobrancasGeradas: false);

        t.InscricoesHabilitadas.Should().BeTrue();
    }

    [Fact]
    public void Desabilitar_sempre_pode_mesmo_com_cobrancas()
    {
        var t = Nova();

        t.AlterarInscricoesHabilitadas(false, cobrancasGeradas: true);

        t.InscricoesHabilitadas.Should().BeFalse();
    }

    [Fact]
    public void A_diretoria_inscreve_com_as_inscricoes_encerradas_mas_nao_em_temporada_encerrada()
    {
        var encerradas = ComInscricoesEncerradas();
        encerradas.Invoking(x => x.ValidarAceitaInscricoes()).Should().Throw<DomainException>("a inscrição pública está fechada");
        encerradas.Invoking(x => x.ValidarAceitaInscricaoDaDiretoria()).Should().NotThrow("a diretoria pode inscrever mesmo assim");

        var fim = ComTabelaGerada();
        fim.Iniciar();
        fim.Encerrar(0);
        fim.Invoking(x => x.ValidarAceitaInscricaoDaDiretoria()).Should().Throw<DomainException>().WithMessage("*encerrada*");
    }

    [Fact]
    public void Datas_da_competicao_sao_opcionais()
    {
        var sem = new Temporada(Guid.NewGuid(), 2026, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));
        var com = new Temporada(Guid.NewGuid(), 2026, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1),
            new DateTime(2026, 3, 1), new DateTime(2026, 6, 30));

        sem.DataInicioCompeticao.Should().BeNull();
        sem.DataFimCompeticao.Should().BeNull();
        com.DataInicioCompeticao.Should().Be(new DateTime(2026, 3, 1));
        com.DataFimCompeticao.Should().Be(new DateTime(2026, 6, 30));
    }

    [Fact]
    public void Competicao_nao_pode_terminar_antes_de_comecar()
    {
        var criar = () => new Temporada(Guid.NewGuid(), 2026, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1),
            new DateTime(2026, 6, 30), new DateTime(2026, 3, 1));

        criar.Should().Throw<DomainException>().WithMessage("*fim da competição*");
    }

    [Fact]
    public void Alterar_datas_valida_e_aceita_limpar_as_da_competicao()
    {
        var t = new Temporada(Guid.NewGuid(), 2026, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1),
            new DateTime(2026, 3, 1), new DateTime(2026, 6, 30));

        t.AlterarDatas(new DateTime(2026, 1, 5), new DateTime(2026, 2, 10), null, null);

        t.DataInicioInscricoes.Should().Be(new DateTime(2026, 1, 5));
        t.DataFimInscricoes.Should().Be(new DateTime(2026, 2, 10));
        t.DataInicioCompeticao.Should().BeNull();
        t.DataFimCompeticao.Should().BeNull();

        t.Invoking(x => x.AlterarDatas(new DateTime(2026, 2, 10), new DateTime(2026, 1, 5), null, null))
            .Should().Throw<DomainException>("a janela de inscrições não pode terminar antes de começar");
    }

    [Fact]
    public void Temporada_encerrada_nao_aceita_mudar_datas()
    {
        var t = ComTabelaGerada();
        t.Iniciar();
        t.Encerrar(0);

        t.Invoking(x => x.AlterarDatas(new DateTime(2026, 1, 5), new DateTime(2026, 2, 10), null, null))
            .Should().Throw<DomainException>().WithMessage("*encerrada*");
    }
}
