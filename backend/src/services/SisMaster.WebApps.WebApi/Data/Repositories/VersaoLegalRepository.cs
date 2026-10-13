using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class VersaoLegalRepository : IVersaoLegalRepository
{
    private readonly CampeonatoDbContext _context;

    public VersaoLegalRepository(CampeonatoDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    public async Task<IReadOnlyList<VersaoLegal>> ListarAsync(Guid associacaoId, CancellationToken ct = default) =>
        await _context.VersoesLegais.AsNoTracking().Where(v => v.AssociacaoId == associacaoId).OrderByDescending(v => v.Numero).ToListAsync(ct);

    public async Task<VersaoLegal?> ObterVigenteAsync(Guid associacaoId, CancellationToken ct = default) =>
        await _context.VersoesLegais.AsNoTracking().Where(v => v.AssociacaoId == associacaoId).OrderByDescending(v => v.Numero).FirstOrDefaultAsync(ct);

    public void Adicionar(VersaoLegal versao) => _context.VersoesLegais.Add(versao);

    public void Dispose() => _context.Dispose();
}
