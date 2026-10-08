using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

public enum TipoDesconto
{
    Percentual,
    Valor
}

/// <summary>
/// Desconto de quem joga exatamente uma combinação de categorias da temporada (ex.: M40+ e M55+ tem um desconto;
/// M30+, M40+ e M55+ outro; M30+ e M40+ outro ainda), aplicado sobre o total das categorias cobradas.
/// A combinação é um conjunto: a ordem não importa.
/// </summary>
public class DescontoPorCombinacao : Entity
{
    public Guid TemporadaId { get; private set; }

    /// <summary>Ids das categorias da temporada (<c>TemporadaCategoria</c>), ordenados e separados por vírgula.</summary>
    public string Combinacao { get; private set; } = string.Empty;
    public TipoDesconto Tipo { get; private set; }
    public decimal Valor { get; private set; }

    public IReadOnlyList<Guid> TemporadaCategoriaIds => Combinacao.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();

    protected DescontoPorCombinacao() { }

    internal DescontoPorCombinacao(Guid temporadaId, IReadOnlyCollection<Guid> temporadaCategoriaIds, TipoDesconto tipo, decimal valor)
    {
        TemporadaId = temporadaId;
        Combinacao = ChaveDa(temporadaCategoriaIds);
        Atualizar(tipo, valor);
    }

    internal static string ChaveDa(IEnumerable<Guid> ids) => string.Join(',', ids.Distinct().OrderBy(i => i));

    internal void Atualizar(TipoDesconto tipo, decimal valor)
    {
        Validacoes.ValidarSeMenorQue(valor, 0m, "Valor do desconto não pode ser negativo");
        if (tipo == TipoDesconto.Percentual)
            Validacoes.ValidarMinimoMaximo(valor, 0m, 100m, "Percentual de desconto deve estar entre 0 e 100");
        Tipo = tipo;
        Valor = valor;
    }

    /// <summary>Este desconto vale para quem joga exatamente estas categorias?</summary>
    public bool Cobre(IEnumerable<Guid> temporadaCategoriaIds) => ChaveDa(temporadaCategoriaIds) == Combinacao;

    public decimal Calcular(decimal total)
    {
        var desconto = Tipo == TipoDesconto.Percentual ? Math.Round(total * Valor / 100m, 2) : Valor;
        return Math.Min(desconto, total);
    }
}
