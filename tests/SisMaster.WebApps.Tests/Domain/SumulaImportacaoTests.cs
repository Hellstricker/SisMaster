using FluentAssertions;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;
using Xunit;

namespace SisMaster.WebApps.Tests.Domain;

public class SumulaImportacaoTests
{
    /// <summary>Casa com camisas 1-7 (titulares 1-5) e visitante com 11-17 (titulares 11-15), ainda em preparação.</summary>
    private static Sumula Nova(bool iniciar = false)
    {
        var s = new Sumula(Guid.NewGuid(), Guid.NewGuid(), "Águias", null, Guid.NewGuid(), "Ursos", null);
        foreach (var (lado, primeira) in new[] { (LadoTime.Casa, 1), (LadoTime.Visitante, 11) })
        {
            var rel = Enumerable.Range(0, 7)
                .Select(i => new RelacionadoNovo(Guid.NewGuid(), $"Atleta {primeira + i}", (primeira + i).ToString(), i < 5)).ToList();
            s.SalvarRelacao(lado, "Prof.", null, rel, rel[0].AtletaId);
        }
        if (iniciar) s.Iniciar();
        return s;
    }

    private static Guid J(Sumula s, LadoTime lado, int camisa) =>
        s.TimeDoLado(lado).Jogadores.First(j => j.Numero == camisa.ToString()).Id;

    [Fact]
    public void Importar_substitui_eventos_e_trocas_recalcula_placar_e_encerra()
    {
        var s = Nova(iniciar: true);
        s.RegistrarEvento(J(s, LadoTime.Casa, 1), TipoEvento.Ponto3, 500); // lançado pela mesa, será descartado

        var resultado = s.ImportarJogoRealizado("2888968",
        [
            new EventoImportado(J(s, LadoTime.Casa, 1), TipoEvento.Ponto2, Periodo.Primeiro, 540),
            new EventoImportado(J(s, LadoTime.Visitante, 11), TipoEvento.Ponto3, Periodo.Primeiro, 500),
            new EventoImportado(J(s, LadoTime.Visitante, 11), TipoEvento.FaltaPessoal, Periodo.Segundo, 300),
            new EventoImportado(J(s, LadoTime.Casa, 6), TipoEvento.LanceLivre, Periodo.Prorrogacao, 120)
        ],
        [new TrocaImportada(J(s, LadoTime.Casa, 1), J(s, LadoTime.Casa, 6), Periodo.Segundo, 600)]);

        s.PlacarCasa.Should().Be(3);
        s.PlacarVisitante.Should().Be(3);
        s.FaltasVisitante.Should().Be(1);
        s.Eventos.Should().HaveCount(4);
        s.Status.Should().Be(StatusSumula.Encerrada);
        s.PeriodoAtual.Should().Be(Periodo.Prorrogacao);
        s.CodigoExterno.Should().Be("2888968");
        s.ImportadaEm.Should().NotBeNull();

        resultado.EventosRemovidos.Should().HaveCount(1);
        resultado.EventosNovos.Should().HaveCount(4);
        resultado.SubstituicoesNovas.Should().ContainSingle().Which.NoInicio.Should().BeTrue(); // relógio cheio no período
        s.QuadraDo(s.TimeDoLado(LadoTime.Casa)).Should().Contain(J(s, LadoTime.Casa, 6)).And.NotContain(J(s, LadoTime.Casa, 1));
    }

    [Fact]
    public void Importar_numa_sumula_em_preparacao_inicia_e_encerra()
    {
        var s = Nova();

        s.ImportarJogoRealizado("1", [new EventoImportado(J(s, LadoTime.Casa, 1), TipoEvento.Ponto2, Periodo.Primeiro, 300)], []);

        s.Status.Should().Be(StatusSumula.Encerrada);
        s.PlacarCasa.Should().Be(2);
    }

    [Fact]
    public void Importar_de_novo_substitui_o_que_foi_importado()
    {
        var s = Nova(iniciar: true);
        s.ImportarJogoRealizado("1", [new EventoImportado(J(s, LadoTime.Casa, 1), TipoEvento.Ponto3, Periodo.Primeiro, 300)], []);

        s.ImportarJogoRealizado("1", [new EventoImportado(J(s, LadoTime.Visitante, 11), TipoEvento.Ponto2, Periodo.Primeiro, 300)], []);

        s.PlacarCasa.Should().Be(0);
        s.PlacarVisitante.Should().Be(2);
        s.Eventos.Should().HaveCount(1);
    }

    [Fact]
    public void Troca_de_quem_nao_esta_em_quadra_nao_altera_nada()
    {
        var s = Nova(iniciar: true);
        s.RegistrarEvento(J(s, LadoTime.Casa, 1), TipoEvento.Ponto2, 500);

        var act = () => s.ImportarJogoRealizado("1", [],
            [new TrocaImportada(J(s, LadoTime.Casa, 6), J(s, LadoTime.Casa, 7), Periodo.Segundo, 600)]);

        act.Should().Throw<DomainException>().WithMessage("*não estava em quadra*");
        s.Eventos.Should().HaveCount(1);
        s.Status.Should().Be(StatusSumula.EmAndamento);
        s.CodigoExterno.Should().BeNull();
    }

    [Fact]
    public void Lance_de_jogador_fora_da_relacao_e_recusado()
    {
        var s = Nova(iniciar: true);

        var act = () => s.ImportarJogoRealizado("1", [new EventoImportado(Guid.NewGuid(), TipoEvento.Ponto2, Periodo.Primeiro, 300)], []);

        act.Should().Throw<DomainException>().WithMessage("*não está relacionado*");
    }

    [Fact]
    public void Troca_entre_times_diferentes_e_recusada()
    {
        var s = Nova(iniciar: true);

        var act = () => s.ImportarJogoRealizado("1", [],
            [new TrocaImportada(J(s, LadoTime.Casa, 1), J(s, LadoTime.Visitante, 16), Periodo.Segundo, 600)]);

        act.Should().Throw<DomainException>().WithMessage("*mesmo time*");
    }
}
