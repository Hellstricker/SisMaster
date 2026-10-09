using FluentAssertions;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Participantes;
using Xunit;

namespace SisMaster.WebApps.Tests.Domain;

/// <summary>Estorno de pagamento, desfazer aprovação, reabrir pedido e correção de dados (tela G7).</summary>
public class InscricaoCorrecoesTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 9);

    private static (Inscricao Ficha, InscricaoCategoria Pedido) FichaAprovada(bool pagarTaxa = false)
    {
        var f = new Inscricao(Guid.NewGuid(), Guid.NewGuid(), 180, 80, "Ala", false, null, true);
        var pedido = f.AdicionarCategoria(Guid.NewGuid());
        f.AprovarCategoria(pedido.Id, null, foraDoEsperado: false);
        if (pagarTaxa) f.RegistrarPagamentoTaxa(200m, Hoje);
        return (f, pedido);
    }

    [Fact]
    public void Estornar_a_taxa_devolve_o_pedido_para_aguardando_pagamento_e_mantem_o_historico()
    {
        var (ficha, pedido) = FichaAprovada(pagarTaxa: true);
        pedido.Status.Should().Be(StatusInscricaoCategoria.Efetivada);
        var taxa = ficha.Pagamentos.Single();

        ficha.EstornarPagamento(taxa.Id, "lançada na ficha errada", "usuario-1");

        ficha.TaxaPaga.Should().BeFalse();
        ficha.TotalPago.Should().Be(0m);
        pedido.Status.Should().Be(StatusInscricaoCategoria.AguardandoPagamento);
        ficha.Pagamentos.Should().ContainSingle("o estorno não apaga o lançamento");
        taxa.Ativo.Should().BeFalse();
        taxa.MotivoEstorno.Should().Be("lançada na ficha errada");
        taxa.EstornadoPor.Should().Be("usuario-1");
    }

    [Fact]
    public void Depois_do_estorno_da_taxa_da_para_pagar_de_novo()
    {
        var (ficha, pedido) = FichaAprovada(pagarTaxa: true);
        ficha.EstornarPagamento(ficha.Pagamentos.Single().Id, "valor errado", null);

        ficha.RegistrarPagamentoTaxa(200m, Hoje);

        pedido.Status.Should().Be(StatusInscricaoCategoria.Efetivada);
        ficha.Pagamentos.Should().HaveCount(2);
        ficha.TotalPago.Should().Be(200m, "só o pagamento ativo conta");
    }

    [Fact]
    public void Estornar_o_saldo_reabre_o_saldo_sem_mexer_no_pedido()
    {
        var (ficha, pedido) = FichaAprovada(pagarTaxa: true);
        ficha.GerarCobranca(500m, 0m);
        var saldo = ficha.RegistrarPagamentoSaldo(300m, Hoje);
        ficha.Saldo.Should().Be(0m);

        ficha.EstornarPagamento(saldo.Id, "cheque devolvido", null);

        ficha.Saldo.Should().Be(300m);
        pedido.Status.Should().Be(StatusInscricaoCategoria.Efetivada, "a taxa continua paga");
    }

    [Fact]
    public void Estorno_exige_motivo_e_nao_vale_duas_vezes()
    {
        var (ficha, _) = FichaAprovada(pagarTaxa: true);
        var id = ficha.Pagamentos.Single().Id;

        ficha.Invoking(f => f.EstornarPagamento(id, "  ", null)).Should().Throw<DomainException>().WithMessage("*motivo*");
        ficha.EstornarPagamento(id, "erro", null);
        ficha.Invoking(f => f.EstornarPagamento(id, "de novo", null)).Should().Throw<DomainException>().WithMessage("*já foi estornado*");
        ficha.Invoking(f => f.EstornarPagamento(Guid.NewGuid(), "x", null)).Should().Throw<DomainException>().WithMessage("*não encontrado*");
    }

    [Fact]
    public void Desfazer_aprovacao_volta_para_pendente_e_limpa_a_justificativa()
    {
        var f = new Inscricao(Guid.NewGuid(), Guid.NewGuid(), null, null, null, false, null, true);
        var pedido = f.AdicionarCategoria(Guid.NewGuid());
        f.AprovarCategoria(pedido.Id, "autorizado pela diretoria", foraDoEsperado: true);
        pedido.JustificativaExcecao.Should().NotBeNull();

        f.DesfazerAprovacao(pedido.Id);

        pedido.Status.Should().Be(StatusInscricaoCategoria.Pendente);
        pedido.JustificativaExcecao.Should().BeNull();
    }

    [Fact]
    public void Desfazer_aprovacao_so_de_pedido_aprovado()
    {
        var f = new Inscricao(Guid.NewGuid(), Guid.NewGuid(), null, null, null, false, null, true);
        var pendente = f.AdicionarCategoria(Guid.NewGuid());

        f.Invoking(x => x.DesfazerAprovacao(pendente.Id)).Should().Throw<DomainException>().WithMessage("*aprovado*");
    }

    [Fact]
    public void Aprovar_de_novo_apos_desfazer_com_taxa_paga_efetiva_na_hora()
    {
        var (ficha, pedido) = FichaAprovada(pagarTaxa: true);
        ficha.DesfazerAprovacao(pedido.Id);
        pedido.Status.Should().Be(StatusInscricaoCategoria.Pendente);

        ficha.AprovarCategoria(pedido.Id, null, foraDoEsperado: false);

        pedido.Status.Should().Be(StatusInscricaoCategoria.Efetivada, "a taxa da ficha continua paga");
    }

    [Fact]
    public void Reabrir_so_pedido_recusado_e_volta_para_pendente()
    {
        var f = new Inscricao(Guid.NewGuid(), Guid.NewGuid(), null, null, null, false, null, true);
        var pedido = f.AdicionarCategoria(Guid.NewGuid());
        f.Invoking(x => x.ReabrirPedido(pedido.Id)).Should().Throw<DomainException>().WithMessage("*recusado*");

        pedido.Recusar("fora da idade");
        f.ReabrirPedido(pedido.Id);

        pedido.Status.Should().Be(StatusInscricaoCategoria.Pendente);
    }

    [Fact]
    public void Corrigir_dados_da_ficha_aplica_as_mesmas_regras_do_envio()
    {
        var f = new Inscricao(Guid.NewGuid(), Guid.NewGuid(), 170, 70, "Ala", false, null, true);

        f.AtualizarDados(185, 90, "  Pivô ", true, "Unimed");

        f.AlturaCm.Should().Be(185);
        f.PesoKg.Should().Be(90);
        f.Posicao.Should().Be("Pivô");
        f.PossuiPlanoSaude.Should().BeTrue();
        f.NomePlanoSaude.Should().Be("Unimed");
        f.Invoking(x => x.AtualizarDados(50, 90, null, false, null)).Should().Throw<DomainException>().WithMessage("*Altura*");
        f.Invoking(x => x.AtualizarDados(180, 20, null, false, null)).Should().Throw<DomainException>().WithMessage("*Peso*");
        f.Invoking(x => x.AtualizarDados(180, 80, null, true, " ")).Should().Throw<DomainException>().WithMessage("*plano de saúde*");

        f.AtualizarDados(null, null, null, false, "ignorado");
        f.NomePlanoSaude.Should().BeNull("sem plano, o nome não fica");
    }

    [Fact]
    public void Pessoa_corrige_contato_e_identificacao_com_validacao()
    {
        var p = new Pessoa("Carlos Souza", "39053344705", new DateOnly(1980, 5, 10), Sexo.Masculino, "carlos@exemplo.test");

        p.AtualizarContato("novo@exemplo.test", " 71999990000 ");
        p.Email.Should().Be("novo@exemplo.test");
        p.Telefone.Should().Be("71999990000");
        p.Invoking(x => x.AtualizarContato("sem-arroba", null)).Should().Throw<DomainException>();

        p.CorrigirIdentificacao("Carlos Souza Lima", "111.444.777-35", new DateOnly(1979, 5, 10), Sexo.Masculino);
        p.Nome.Should().Be("Carlos Souza Lima");
        p.Cpf.Numero.Should().Be("11144477735");
        p.Nascimento.Should().Be(new DateOnly(1979, 5, 10));
        p.Invoking(x => x.CorrigirIdentificacao("Ab", "11144477735", new DateOnly(1979, 5, 10), Sexo.Masculino)).Should().Throw<DomainException>();
    }
}
