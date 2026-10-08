using Xunit;
using FluentAssertions;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.Tests.Domain;

public class SumulaTests
{
    private static Sumula Nova() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Águias", "#ff0000", Guid.NewGuid(), "Ursos", null);

    private static List<RelacionadoNovo> Relacao(int quantos, int titulares = 5, int primeiraCamisa = 1) =>
        Enumerable.Range(0, quantos)
            .Select(i => new RelacionadoNovo(Guid.NewGuid(), $"Atleta {i + 1}", (primeiraCamisa + i).ToString(), i < titulares))
            .ToList();

    private static void Completar(Sumula s, LadoTime lado, int quantos = 7)
    {
        var rel = Relacao(quantos);
        s.SalvarRelacao(lado, "Prof. Carlos", null, rel, rel[0].AtletaId);
    }

    [Fact]
    public void Nova_sumula_nasce_em_preparacao_com_os_dois_times_sem_relacao()
    {
        var s = Nova();

        s.Status.Should().Be(StatusSumula.EmPreparacao);
        s.Times.Should().HaveCount(2);
        s.TimeDoLado(LadoTime.Casa).Nome.Should().Be("Águias");
        s.TimeCasaId.Should().Be(s.TimeDoLado(LadoTime.Casa).Id);
        s.TimeDoLado(LadoTime.Casa).Jogadores.Should().BeEmpty();
    }

    [Fact]
    public void Salvar_relacao_substitui_a_anterior_e_define_o_capitao_entre_os_relacionados()
    {
        var s = Nova();
        var primeira = Relacao(8);
        s.SalvarRelacao(LadoTime.Casa, "Prof. Carlos", "Aux", primeira, primeira[2].AtletaId);

        var segunda = Relacao(6);
        var (removidos, adicionados) = s.SalvarRelacao(LadoTime.Casa, "Prof. Carlos", null, segunda, segunda[0].AtletaId);

        removidos.Should().HaveCount(8);
        adicionados.Should().HaveCount(6);
        var time = s.TimeDoLado(LadoTime.Casa);
        time.Jogadores.Should().HaveCount(6);
        time.CapitaoId.Should().Be(time.Jogadores.First(j => j.AtletaId == segunda[0].AtletaId).Id);
        time.AuxiliarTecnico.Should().BeNull();
    }

    [Fact]
    public void Mais_de_12_relacionados_e_recusado()
    {
        var s = Nova();

        var act = () => s.SalvarRelacao(LadoTime.Casa, "T", null, Relacao(13), null);

        act.Should().Throw<DomainException>().WithMessage("*12*");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("00")]
    [InlineData("7")]
    [InlineData("99")]
    public void Camisas_validas_sao_aceitas(string camisa)
    {
        var s = Nova();
        var rel = new List<RelacionadoNovo> { new(Guid.NewGuid(), "A", camisa, false) };

        var act = () => s.SalvarRelacao(LadoTime.Casa, "T", null, rel, null);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("100")]
    [InlineData("07")]
    [InlineData("-1")]
    [InlineData("A")]
    public void Camisas_invalidas_sao_recusadas(string camisa)
    {
        var s = Nova();
        var rel = new List<RelacionadoNovo> { new(Guid.NewGuid(), "A", camisa, false) };

        var act = () => s.SalvarRelacao(LadoTime.Casa, "T", null, rel, null);

        act.Should().Throw<DomainException>().WithMessage("*Camisa*");
    }

    [Fact]
    public void Camisa_repetida_na_equipe_e_recusada_mas_0_e_00_sao_diferentes()
    {
        var s = Nova();
        var repetida = new List<RelacionadoNovo> { new(Guid.NewGuid(), "A", "10", false), new(Guid.NewGuid(), "B", "10", false) };
        var zeros = new List<RelacionadoNovo> { new(Guid.NewGuid(), "A", "0", false), new(Guid.NewGuid(), "B", "00", false) };

        var act1 = () => s.SalvarRelacao(LadoTime.Casa, "T", null, repetida, null);
        var act2 = () => s.SalvarRelacao(LadoTime.Casa, "T", null, zeros, null);

        act1.Should().Throw<DomainException>().WithMessage("*repetida*");
        act2.Should().NotThrow();
    }

    [Fact]
    public void Mais_de_5_titulares_ou_capitao_fora_da_relacao_sao_recusados()
    {
        var s = Nova();

        var muitosTitulares = () => s.SalvarRelacao(LadoTime.Casa, "T", null, Relacao(8, titulares: 6), null);
        var capitaoDeFora = () => s.SalvarRelacao(LadoTime.Casa, "T", null, Relacao(8), Guid.NewGuid());

        muitosTitulares.Should().Throw<DomainException>().WithMessage("*titulares*");
        capitaoDeFora.Should().Throw<DomainException>().WithMessage("*capitão*");
    }

    [Fact]
    public void Atleta_relacionado_duas_vezes_e_recusado()
    {
        var s = Nova();
        var id = Guid.NewGuid();
        var rel = new List<RelacionadoNovo> { new(id, "A", "1", false), new(id, "A", "2", false) };

        var act = () => s.SalvarRelacao(LadoTime.Casa, "T", null, rel, null);

        act.Should().Throw<DomainException>().WithMessage("*duas vezes*");
    }

    [Fact]
    public void Pendencias_listam_o_que_falta_para_iniciar()
    {
        var s = Nova();
        s.SalvarRelacao(LadoTime.Casa, null, null, Relacao(3, titulares: 3), null);

        var pendencias = s.TimeDoLado(LadoTime.Casa).Pendencias();

        pendencias.Should().Contain(p => p.Contains("mínimo de 4"));
        pendencias.Should().Contain("definir o capitão");
        pendencias.Should().NotContain(p => p.Contains("técnico"));   // o técnico é opcional
    }

    [Fact]
    public void Iniciar_exige_as_duas_relacoes_completas_e_trava_a_relacao()
    {
        var s = Nova();
        Completar(s, LadoTime.Casa);

        var semVisitante = () => s.Iniciar();
        semVisitante.Should().Throw<DomainException>().WithMessage("*Ursos*");

        Completar(s, LadoTime.Visitante);
        s.Iniciar();

        s.Status.Should().Be(StatusSumula.EmAndamento);
        var alterar = () => s.SalvarRelacao(LadoTime.Casa, "T", null, Relacao(5), null);
        alterar.Should().Throw<DomainException>().WithMessage("*antes de a súmula ser iniciada*");
    }

    [Fact]
    public void Sem_iniciar_nao_ha_cronometro_nem_eventos()
    {
        var s = Nova();
        Completar(s, LadoTime.Casa);

        var cronometro = () => s.IniciarCronometro();
        var evento = () => s.RegistrarEvento(s.TimeDoLado(LadoTime.Casa).Jogadores.First().Id, TipoEvento.Ponto2, 10);

        cronometro.Should().Throw<DomainException>().WithMessage("*Inicie a súmula*");
        evento.Should().Throw<DomainException>().WithMessage("*Inicie a súmula*");
    }

    [Fact]
    public void Evento_de_jogador_nao_relacionado_e_recusado_e_o_placar_vai_para_o_time_do_jogador()
    {
        var s = Nova();
        Completar(s, LadoTime.Casa);
        Completar(s, LadoTime.Visitante);
        s.Iniciar();
        var jogadorCasa = s.TimeDoLado(LadoTime.Casa).Jogadores.First();
        var jogadorVisitante = s.TimeDoLado(LadoTime.Visitante).Jogadores.First();

        s.RegistrarEvento(jogadorCasa.Id, TipoEvento.Ponto3, 30);
        s.RegistrarEvento(jogadorVisitante.Id, TipoEvento.Ponto2, 45);
        var estranho = () => s.RegistrarEvento(Guid.NewGuid(), TipoEvento.Ponto2, 50);

        s.PlacarCasa.Should().Be(3);
        s.PlacarVisitante.Should().Be(2);
        estranho.Should().Throw<DomainException>().WithMessage("*não está relacionado*");

        s.DesfazerUltimoEvento();
        s.PlacarVisitante.Should().Be(0);
    }

    [Fact]
    public void Encerrar_so_depois_de_iniciada()
    {
        var s = Nova();

        var act = () => s.Encerrar();

        act.Should().Throw<DomainException>().WithMessage("*não foi iniciada*");
    }
    private static Sumula Iniciada(int relacionados = 8)
    {
        var s = Nova();
        Completar(s, LadoTime.Casa, relacionados);
        Completar(s, LadoTime.Visitante, relacionados);
        s.Iniciar();
        return s;
    }

    private static List<Jogador> Casa(Sumula s) => s.TimeDoLado(LadoTime.Casa).Jogadores.ToList();

    [Fact]
    public void Quadra_comeca_com_os_titulares_e_muda_so_por_substituicao()
    {
        var s = Iniciada();
        var j = Casa(s);

        s.QuadraDo(s.TimeDoLado(LadoTime.Casa)).Should().BeEquivalentTo(j.Take(5).Select(x => x.Id));
        s.RegistrarSubstituicao(j[0].Id, j[6].Id, 600);

        var quadra = s.QuadraDo(s.TimeDoLado(LadoTime.Casa));
        quadra.Should().Contain(j[6].Id).And.NotContain(j[0].Id).And.HaveCount(5);
    }

    [Fact]
    public void Substituicao_antes_do_relogio_correr_vale_como_inicio_do_periodo()
    {
        var s = Iniciada();
        var j = Casa(s);

        s.RelogioNoInicio.Should().BeTrue();
        s.RegistrarSubstituicao(j[0].Id, j[6].Id, 600).NoInicio.Should().BeTrue();

        s.IniciarCronometro();
        s.RelogioNoInicio.Should().BeFalse();
        s.RegistrarSubstituicao(j[1].Id, j[7].Id, 540).NoInicio.Should().BeFalse();
    }

    [Fact]
    public void Substituicao_so_entre_quem_esta_em_quadra_e_quem_esta_no_banco_do_mesmo_time()
    {
        var s = Iniciada();
        var j = Casa(s);
        var visitante = s.TimeDoLado(LadoTime.Visitante).Jogadores.First();

        var sai_do_banco = () => s.RegistrarSubstituicao(j[6].Id, j[7].Id, 600);
        var entra_quem_ja_joga = () => s.RegistrarSubstituicao(j[0].Id, j[1].Id, 600);
        var outro_time = () => s.RegistrarSubstituicao(j[0].Id, visitante.Id, 600);

        sai_do_banco.Should().Throw<DomainException>().WithMessage("*não está em quadra*");
        entra_quem_ja_joga.Should().Throw<DomainException>().WithMessage("*já está em quadra*");
        outro_time.Should().Throw<DomainException>().WithMessage("*mesmo time*");
    }

    [Fact]
    public void Substituicao_exige_sumula_em_andamento_ou_intervalo()
    {
        var preparada = Nova();
        Completar(preparada, LadoTime.Casa);
        var j = Casa(preparada);

        var act = () => preparada.RegistrarSubstituicao(j[0].Id, j[6].Id, 600);

        act.Should().Throw<DomainException>().WithMessage("*em andamento ou no intervalo*");
    }

    [Fact]
    public void Desfazer_remove_a_ultima_substituicao_e_devolve_a_quadra()
    {
        var s = Iniciada();
        var j = Casa(s);
        s.RegistrarSubstituicao(j[0].Id, j[6].Id, 600);
        s.RegistrarSubstituicao(j[1].Id, j[7].Id, 600);

        s.DesfazerUltimaSubstituicao().JogadorEntraId.Should().Be(j[7].Id);

        s.QuadraDo(s.TimeDoLado(LadoTime.Casa)).Should().Contain(j[1].Id).And.Contain(j[6].Id);
        s.Substituicoes.Should().HaveCount(1);
    }

    [Fact]
    public void Periodos_encerrados_acompanham_o_andamento_e_nunca_passam_de_4()
    {
        var s = Iniciada();
        s.PeriodosNormaisEncerrados.Should().Be(0);   // 1º em andamento

        s.AvancarPeriodo();
        s.PeriodosNormaisEncerrados.Should().Be(1);   // intervalo antes do 2º
        s.AvancarPeriodo(); s.AvancarPeriodo();
        s.AvancarPeriodo();                           // prorrogação
        s.PeriodosNormaisEncerrados.Should().Be(4);

        s.Encerrar();
        s.PeriodosNormaisEncerrados.Should().Be(4);
    }

    [Fact]
    public void Encerrar_no_quarto_periodo_conta_os_4_periodos()
    {
        var s = Iniciada();
        s.AvancarPeriodo(); s.AvancarPeriodo(); s.AvancarPeriodo(); // 4º em andamento

        s.Encerrar();

        s.PeriodosNormaisEncerrados.Should().Be(4);
    }
    // ---------- mínimo de 4 e atletas acrescentados depois do início ----------

    private static void CompletarComTitulares(Sumula s, LadoTime lado, int quantos)
    {
        var rel = Relacao(quantos, titulares: Math.Min(5, quantos));
        s.SalvarRelacao(lado, null, null, rel, rel[0].AtletaId);
    }

    [Fact]
    public void Com_4_relacionados_todos_titulares_a_sumula_pode_ser_iniciada()
    {
        var s = Nova();
        CompletarComTitulares(s, LadoTime.Casa, 4);
        CompletarComTitulares(s, LadoTime.Visitante, 4);

        s.Iniciar();

        s.Status.Should().Be(StatusSumula.EmAndamento);
        s.QuadraDo(s.TimeDoLado(LadoTime.Casa)).Should().HaveCount(4);
    }

    [Fact]
    public void Com_3_relacionados_nao_inicia_e_com_4_exige_que_todos_sejam_titulares()
    {
        var s = Nova();
        s.SalvarRelacao(LadoTime.Casa, null, null, Relacao(3, titulares: 3), null);
        s.TimeDoLado(LadoTime.Casa).Pendencias().Should().Contain(p => p.Contains("mínimo de 4"));

        var quatro = Relacao(4, titulares: 3);
        s.SalvarRelacao(LadoTime.Casa, null, null, quatro, quatro[0].AtletaId);
        s.TimeDoLado(LadoTime.Casa).Pendencias().Should().ContainSingle().Which.Should().Contain("4 relacionados como titulares");
    }

    [Fact]
    public void Com_5_ou_mais_continuam_exigindo_exatamente_5_titulares()
    {
        var s = Nova();
        var rel = Relacao(8, titulares: 4);
        s.SalvarRelacao(LadoTime.Casa, null, null, rel, rel[0].AtletaId);

        s.TimeDoLado(LadoTime.Casa).Pendencias().Should().ContainSingle().Which.Should().Contain("exatamente 5 titulares");
    }

    private static Sumula IniciadaCom4()
    {
        var s = Nova();
        CompletarComTitulares(s, LadoTime.Casa, 4);
        CompletarComTitulares(s, LadoTime.Visitante, 4);
        s.Iniciar();
        return s;
    }

    [Fact]
    public void Atleta_acrescentado_depois_do_inicio_entra_no_banco_com_o_periodo_de_chegada()
    {
        var s = IniciadaCom4();

        var j = s.AcrescentarJogador(LadoTime.Casa, new RelacionadoNovo(Guid.NewGuid(), "Atrasado", "77", true));

        j.Titular.Should().BeFalse();                       // sempre entra no banco
        j.ChegouNoPeriodo.Should().Be(1);
        j.ChegouNoInicio.Should().BeTrue();                 // relógio ainda parado
        s.QuadraDo(s.TimeDoLado(LadoTime.Casa)).Should().NotContain(j.Id);
        s.TimeDoLado(LadoTime.Casa).Jogadores.Should().HaveCount(5);
    }

    [Fact]
    public void Acrescentar_so_depois_de_iniciada_e_antes_de_encerrada()
    {
        var antes = Nova();
        CompletarComTitulares(antes, LadoTime.Casa, 4);
        var cedo = () => antes.AcrescentarJogador(LadoTime.Casa, new RelacionadoNovo(Guid.NewGuid(), "A", "20", false));
        cedo.Should().Throw<DomainException>().WithMessage("*em preparação*");

        var s = IniciadaCom4();
        s.Encerrar();
        var tarde = () => s.AcrescentarJogador(LadoTime.Casa, new RelacionadoNovo(Guid.NewGuid(), "A", "20", false));
        tarde.Should().Throw<DomainException>().WithMessage("*encerrada*");
    }

    [Fact]
    public void Acrescentar_respeita_camisa_unica_atleta_unico_e_maximo_de_12()
    {
        var s = IniciadaCom4();
        var casa = Casa(s);

        var camisaRepetida = () => s.AcrescentarJogador(LadoTime.Casa, new RelacionadoNovo(Guid.NewGuid(), "X", casa[0].Numero, false));
        var camisaInvalida = () => s.AcrescentarJogador(LadoTime.Casa, new RelacionadoNovo(Guid.NewGuid(), "X", "100", false));
        var atletaRepetido = () => s.AcrescentarJogador(LadoTime.Casa, new RelacionadoNovo(casa[0].AtletaId, "X", "55", false));
        camisaRepetida.Should().Throw<DomainException>().WithMessage("*em uso*");
        camisaInvalida.Should().Throw<DomainException>().WithMessage("*Camisa inválida*");
        atletaRepetido.Should().Throw<DomainException>().WithMessage("*já está relacionado*");

        for (var n = 0; n < 8; n++)
            s.AcrescentarJogador(LadoTime.Casa, new RelacionadoNovo(Guid.NewGuid(), $"Extra {n}", $"{20 + n}", false));
        var vigesimo = () => s.AcrescentarJogador(LadoTime.Casa, new RelacionadoNovo(Guid.NewGuid(), "Demais", "60", false));
        vigesimo.Should().Throw<DomainException>().WithMessage("*No máximo 12*");
    }

    [Fact]
    public void Quadra_incompleta_aceita_entrada_sem_saida_e_quadra_completa_nao()
    {
        var s = IniciadaCom4();
        var atrasado = s.AcrescentarJogador(LadoTime.Casa, new RelacionadoNovo(Guid.NewGuid(), "Atrasado", "77", false));

        s.RegistrarSubstituicao(null, atrasado.Id, 600);

        s.QuadraDo(s.TimeDoLado(LadoTime.Casa)).Should().HaveCount(5).And.Contain(atrasado.Id);

        var outro = s.AcrescentarJogador(LadoTime.Casa, new RelacionadoNovo(Guid.NewGuid(), "Outro", "78", false));
        var completa = () => s.RegistrarSubstituicao(null, outro.Id, 600);
        completa.Should().Throw<DomainException>().WithMessage("*quadra está completa*");
    }

    [Fact]
    public void Rodizio_de_quem_chegou_depois_ignora_os_periodos_em_que_ainda_nao_estava()
    {
        var s = IniciadaCom4();
        s.AvancarPeriodo();                                  // 1º encerrado; intervalo antes do 2º
        s.AvancarPeriodo();                                  // 2º encerrado; intervalo antes do 3º
        var atrasado = s.AcrescentarJogador(LadoTime.Casa, new RelacionadoNovo(Guid.NewGuid(), "Atrasado", "77", false));
        s.AvancarPeriodo();                                  // 3º encerrado

        var rodizio = s.RodizioDo(s.TimeDoLado(LadoTime.Casa)).Single(r => r.JogadorId == atrasado.Id);

        rodizio.Periodos.Should().Equal(EstadoPeriodo.Ausente, EstadoPeriodo.Ausente, EstadoPeriodo.Fora);
    }
}
