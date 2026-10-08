using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

public interface ITemporadaRepository : IRepository<Temporada>
{
    Task<Temporada?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Categoria da temporada com Temporada, Campeonato e Descontos carregados.</summary>
    Task<TemporadaCategoria?> ObterCategoriaPorIdAsync(Guid temporadaCategoriaId, CancellationToken ct = default);
    void AdicionarCategoria(TemporadaCategoria categoria);
    void RemoverCategoria(TemporadaCategoria categoria);

    /// <summary>O que depende de uma categoria da temporada (impede a remoção).</summary>
    Task<UsoDaCategoria> ObterUsoDaCategoriaAsync(Guid temporadaCategoriaId, CancellationToken ct = default);
    void AdicionarDesconto(DescontoPorCombinacao desconto);
    void RemoverDesconto(DescontoPorCombinacao desconto);
}

/// <summary>Quantos pedidos de inscrição (inclusive recusados, que ficam como histórico), equipes e fases usam a categoria.</summary>
public sealed record UsoDaCategoria(int Pedidos, int Equipes, int Fases)
{
    public bool EmUso => Pedidos + Equipes + Fases > 0;
}
