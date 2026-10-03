using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Time;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class TimeRepository : ITimeRepository
{
    private readonly CampeonatoDbContext _context;

    public TimeRepository(CampeonatoDbContext context)
    {
        _context = context;
    }

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Time?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Times
            .Include(t => t.Jogadores)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IEnumerable<Time>> ListarAsync(CancellationToken ct = default) =>
        await _context.Times.AsNoTracking().ToListAsync(ct);

    public async Task<Jogador?> ObterJogadorPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Jogadores
            .Include(j => j.Time)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

    public void Adicionar(Time time) => _context.Times.Add(time);
    public void Atualizar(Time time) => _context.Times.Update(time);
}
