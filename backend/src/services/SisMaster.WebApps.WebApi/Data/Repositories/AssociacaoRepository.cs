using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class AssociacaoRepository : IAssociacaoRepository
{
    private readonly CampeonatoDbContext _context;

    public AssociacaoRepository(CampeonatoDbContext context)
    {
        _context = context;
    }

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Associacao?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Associacoes
            .Include(a => a.Campeonatos)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IEnumerable<Associacao>> ListarAsync(CancellationToken ct = default) =>
        await _context.Associacoes
            .AsNoTracking()
            .Include(a => a.Campeonatos)
            .ToListAsync(ct);

    public void Adicionar(Associacao associacao) => _context.Associacoes.Add(associacao);
    public void Atualizar(Associacao associacao) => _context.Associacoes.Update(associacao);
    public void AdicionarCampeonato(Campeonato campeonato) => _context.Campeonatos.Add(campeonato);
}
