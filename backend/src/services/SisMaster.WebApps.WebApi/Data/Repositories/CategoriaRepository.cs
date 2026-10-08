using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class CategoriaRepository : ICategoriaRepository
{
    private readonly CampeonatoDbContext _context;

    public CategoriaRepository(CampeonatoDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    public async Task<IEnumerable<Categoria>> ObterPorAssociacaoAsync(Guid associacaoId, CancellationToken ct = default) =>
        await _context.Categorias
            .AsNoTracking()
            .Where(c => c.AssociacaoId == associacaoId)
            .OrderBy(c => c.Nome)
            .ToListAsync(ct);

    public async Task<bool> ExisteComNomeAsync(Guid associacaoId, string nome, CancellationToken ct = default) =>
        await _context.Categorias.AnyAsync(c => c.AssociacaoId == associacaoId && c.Nome == nome, ct);

    public async Task<Categoria?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Categorias.FindAsync([id], ct);

    public void Adicionar(Categoria categoria) =>
        _context.Categorias.Add(categoria);

    public async Task<IReadOnlyDictionary<Guid, int>> ContarUsoPorAssociacaoAsync(Guid associacaoId, CancellationToken ct = default) =>
        await _context.TemporadaCategorias
            .Where(tc => tc.Categoria.AssociacaoId == associacaoId)
            .GroupBy(tc => tc.CategoriaId)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

    public async Task<int> ContarUsoAsync(Guid categoriaId, CancellationToken ct = default) =>
        await _context.TemporadaCategorias.CountAsync(tc => tc.CategoriaId == categoriaId, ct);

    public void Remover(Categoria categoria) => _context.Categorias.Remove(categoria);

    public void Dispose() => _context.Dispose();
}
