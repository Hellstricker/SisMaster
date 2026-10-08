using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class CampeonatoRepository : ICampeonatoRepository
{
    private readonly CampeonatoDbContext _context;

    public CampeonatoRepository(CampeonatoDbContext context)
    {
        _context = context;
    }

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Campeonato?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Campeonatos
            .Include(c => c.Temporadas)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public void Atualizar(Campeonato campeonato) => _context.Campeonatos.Update(campeonato);

    public void AdicionarTemporada(Temporada temporada) => _context.Temporadas.Add(temporada);
}
