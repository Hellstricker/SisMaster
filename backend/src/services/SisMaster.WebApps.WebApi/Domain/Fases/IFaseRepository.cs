using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Fases;

public interface IFaseRepository : IRepository<Fase>
{
    /// <summary>Fase com TemporadaCategoria.Temporada carregados (rastreada).</summary>
    Task<Fase?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Fases da categoria na ordem da sequência (rastreadas).</summary>
    Task<List<Fase>> ListarPorTemporadaCategoriaAsync(Guid temporadaCategoriaId, CancellationToken ct = default);

    /// <summary>Todas as fases da temporada (somente leitura), na ordem da sequência.</summary>
    Task<IReadOnlyList<Fase>> ListarPorTemporadaAsync(Guid temporadaId, CancellationToken ct = default);

    Task<bool> ExisteNaTemporadaAsync(Guid temporadaId, CancellationToken ct = default);
    Task<bool> ExisteDependenteAsync(Guid faseId, CancellationToken ct = default);

    void Adicionar(Fase fase);
    void Remover(Fase fase);
}
