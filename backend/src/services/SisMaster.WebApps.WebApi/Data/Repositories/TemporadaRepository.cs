using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class TemporadaRepository : ITemporadaRepository
{
    private readonly CampeonatoDbContext _context;

    public TemporadaRepository(CampeonatoDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Temporada?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Temporadas
            .Include(t => t.Campeonato)
            .Include(t => t.Descontos)
            .Include(t => t.Categorias)
                .ThenInclude(tc => tc.Categoria)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TemporadaCategoria?> ObterCategoriaPorIdAsync(Guid temporadaCategoriaId, CancellationToken ct = default) =>
        await _context.TemporadaCategorias
            .AsNoTracking()
            .Include(tc => tc.Temporada).ThenInclude(t => t.Campeonato)
            .Include(tc => tc.Temporada).ThenInclude(t => t.Descontos)
            .FirstOrDefaultAsync(tc => tc.Id == temporadaCategoriaId, ct);

    public void AdicionarCategoria(TemporadaCategoria categoria) =>
        _context.TemporadaCategorias.Add(categoria);

    public void RemoverCategoria(TemporadaCategoria categoria) =>
        _context.TemporadaCategorias.Remove(categoria);

    public async Task<UsoDaCategoria> ObterUsoDaCategoriaAsync(Guid temporadaCategoriaId, CancellationToken ct = default) =>
        new(await _context.InscricoesCategorias.CountAsync(i => i.TemporadaCategoriaId == temporadaCategoriaId, ct),
            await _context.Equipes.CountAsync(e => e.TemporadaCategoriaId == temporadaCategoriaId, ct),
            await _context.Fases.CountAsync(f => f.TemporadaCategoriaId == temporadaCategoriaId, ct));

    public void AdicionarDesconto(DescontoPorCombinacao desconto) =>
        _context.DescontosPorCombinacao.Add(desconto);

    public void RemoverDesconto(DescontoPorCombinacao desconto) =>
        _context.DescontosPorCombinacao.Remove(desconto);

    public void Dispose() => _context.Dispose();
}
