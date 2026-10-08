using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Associacao;

public interface ICategoriaRepository : IRepository<Categoria>
{
    Task<IEnumerable<Categoria>> ObterPorAssociacaoAsync(Guid associacaoId, CancellationToken ct = default);
    Task<bool> ExisteComNomeAsync(Guid associacaoId, string nome, CancellationToken ct = default);
    Task<Categoria?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Em quantas temporadas cada categoria da associação está vinculada (só as usadas aparecem).</summary>
    Task<IReadOnlyDictionary<Guid, int>> ContarUsoPorAssociacaoAsync(Guid associacaoId, CancellationToken ct = default);

    Task<int> ContarUsoAsync(Guid categoriaId, CancellationToken ct = default);

    void Adicionar(Categoria categoria);
    void Remover(Categoria categoria);
}
