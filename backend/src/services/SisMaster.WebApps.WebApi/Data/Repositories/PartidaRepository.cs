using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Partida;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class PartidaRepository : IPartidaRepository
{
    private readonly CampeonatoDbContext _context;

    public PartidaRepository(CampeonatoDbContext context)
    {
        _context = context;
    }

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Partida?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Partidas
            .Include(p => p.Eventos)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IEnumerable<Partida>> ListarAsync(CancellationToken ct = default) =>
        await _context.Partidas.AsNoTracking().ToListAsync(ct);

    public void Adicionar(Partida partida) => _context.Partidas.Add(partida);
    public void Atualizar(Partida partida) { }
    public void AdicionarEvento(EventoPartida evento) => _context.EventosPartida.Add(evento);
}
