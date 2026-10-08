using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Fases;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class FaseRepository : IFaseRepository
{
    private readonly CampeonatoDbContext _context;

    public FaseRepository(CampeonatoDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Fase?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Fases
            .Include(f => f.TemporadaCategoria).ThenInclude(tc => tc.Temporada)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<List<Fase>> ListarPorTemporadaCategoriaAsync(Guid temporadaCategoriaId, CancellationToken ct = default) =>
        await _context.Fases
            .Where(f => f.TemporadaCategoriaId == temporadaCategoriaId)
            .OrderBy(f => f.Ordem)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Fase>> ListarPorTemporadaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.Fases
            .AsNoTracking()
            .Where(f => f.TemporadaCategoria.TemporadaId == temporadaId)
            .OrderBy(f => f.Ordem)
            .ToListAsync(ct);

    public async Task<bool> ExisteNaTemporadaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.Fases.AnyAsync(f => f.TemporadaCategoria.TemporadaId == temporadaId, ct);

    public async Task<bool> ExisteDependenteAsync(Guid faseId, CancellationToken ct = default) =>
        await _context.Fases.AnyAsync(f => f.FaseAnteriorId == faseId, ct);

    public void Adicionar(Fase fase) => _context.Fases.Add(fase);
    public void Remover(Fase fase) => _context.Fases.Remove(fase);

    public void Dispose() => _context.Dispose();
}
