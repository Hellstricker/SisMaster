using Xunit;
using FluentAssertions;
using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.Tests.Domain;

public class DescontoPorCombinacaoTests
{
    private static readonly Guid AssociacaoId = Guid.NewGuid();

    private static (Temporada Temporada, Guid M30, Guid M40, Guid M55) NovaTemporada()
    {
        var t = new Temporada(Guid.NewGuid(), 2026, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));
        Guid Adicionar(string nome, int idade) => t.AdicionarCategoria(new Categoria(AssociacaoId, nome, idade)).Id;
        return (t, Adicionar("M30+", 30), Adicionar("M40+", 40), Adicionar("M55+", 55));
    }

    [Fact]
    public void Cada_combinacao_de_categorias_tem_o_seu_desconto()
    {
        var (t, m30, m40, m55) = NovaTemporada();
        t.DefinirDesconto([m40, m55], TipoDesconto.Percentual, 10);
        t.DefinirDesconto([m30, m40, m55], TipoDesconto.Percentual, 20);
        t.DefinirDesconto([m30, m40], TipoDesconto.Valor, 50);

        t.CalcularDesconto([m40, m55], 400m).Should().Be(40m);          // 10%
        t.CalcularDesconto([m30, m40, m55], 600m).Should().Be(120m);    // 20%
        t.CalcularDesconto([m30, m40], 400m).Should().Be(50m);          // R$ 50
    }

    [Fact]
    public void A_ordem_das_categorias_nao_importa()
    {
        var (t, m30, m40, m55) = NovaTemporada();
        t.DefinirDesconto([m55, m40], TipoDesconto.Percentual, 10);

        t.CalcularDesconto([m40, m55], 200m).Should().Be(20m);
        t.CalcularDesconto([m55, m40], 200m).Should().Be(20m);
    }

    [Fact]
    public void Combinacao_sem_desconto_configurado_nao_recebe_desconto_nem_o_de_outra_combinacao()
    {
        var (t, m30, m40, m55) = NovaTemporada();
        t.DefinirDesconto([m40, m55], TipoDesconto.Percentual, 10);

        t.CalcularDesconto([m30, m55], 300m).Should().Be(0m);            // outra combinação de 2
        t.CalcularDesconto([m30, m40, m55], 300m).Should().Be(0m);       // contém a combinação, mas não é igual
        t.CalcularDesconto([m40], 100m).Should().Be(0m);                 // uma categoria só
    }

    [Fact]
    public void Definir_de_novo_a_mesma_combinacao_atualiza_em_vez_de_duplicar()
    {
        var (t, m30, m40, _) = NovaTemporada();
        var (primeiro, novo1) = t.DefinirDesconto([m30, m40], TipoDesconto.Percentual, 10);
        var (segundo, novo2) = t.DefinirDesconto([m40, m30], TipoDesconto.Valor, 25);

        novo1.Should().BeTrue();
        novo2.Should().BeFalse();
        segundo.Should().BeSameAs(primeiro);
        t.Descontos.Should().ContainSingle();
        t.CalcularDesconto([m30, m40], 100m).Should().Be(25m);
    }

    [Fact]
    public void Desconto_nunca_passa_do_total()
    {
        var (t, m30, m40, _) = NovaTemporada();
        t.DefinirDesconto([m30, m40], TipoDesconto.Valor, 500);

        t.CalcularDesconto([m30, m40], 120m).Should().Be(120m);
    }

    [Fact]
    public void Combinacao_precisa_de_ao_menos_2_categorias_da_propria_temporada()
    {
        var (t, m30, _, _) = NovaTemporada();

        ((Action)(() => t.DefinirDesconto([m30], TipoDesconto.Percentual, 10))).Should().Throw<DomainException>().WithMessage("*ao menos 2*");
        ((Action)(() => t.DefinirDesconto([m30, m30], TipoDesconto.Percentual, 10))).Should().Throw<DomainException>().WithMessage("*ao menos 2*");
        ((Action)(() => t.DefinirDesconto([m30, Guid.NewGuid()], TipoDesconto.Percentual, 10))).Should().Throw<DomainException>().WithMessage("*não pertence*");
        var outraTemporada = NovaTemporada();
        ((Action)(() => t.DefinirDesconto([m30, outraTemporada.M40], TipoDesconto.Percentual, 10))).Should().Throw<DomainException>().WithMessage("*não pertence*");
    }

    [Fact]
    public void Remover_desconto_tira_a_combinacao()
    {
        var (t, m30, m40, _) = NovaTemporada();
        var (d, _) = t.DefinirDesconto([m30, m40], TipoDesconto.Percentual, 10);

        t.RemoverDesconto(d.Id);

        t.Descontos.Should().BeEmpty();
        t.CalcularDesconto([m30, m40], 100m).Should().Be(0m);
        ((Action)(() => t.RemoverDesconto(d.Id))).Should().Throw<DomainException>();
    }

    [Fact]
    public void Percentual_acima_de_100_e_recusado()
    {
        var (t, m30, m40, _) = NovaTemporada();

        ((Action)(() => t.DefinirDesconto([m30, m40], TipoDesconto.Percentual, 120))).Should().Throw<DomainException>();
    }
}
