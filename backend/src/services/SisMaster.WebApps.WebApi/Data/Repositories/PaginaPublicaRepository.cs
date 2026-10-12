using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Associacao;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class PaginaPublicaRepository : IPaginaPublicaRepository
{
    private readonly CampeonatoDbContext _context;

    public PaginaPublicaRepository(CampeonatoDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    public async Task<PaginaPublica?> ObterDaAssociacaoAsync(Guid associacaoId, CancellationToken ct = default) =>
        await _context.PaginasPublicas.Include(p => p.Diretoria).FirstOrDefaultAsync(p => p.AssociacaoId == associacaoId, ct);

    public void Adicionar(PaginaPublica pagina) => _context.PaginasPublicas.Add(pagina);

    // Filhos novos entram por Add explícito (Update/Modified em um agregado com filhos novos os trataria como existentes).
    public void AdicionarMembros(IEnumerable<MembroDaDiretoria> membros) => _context.MembrosDaDiretoria.AddRange(membros);
    public void RemoverMembros(IEnumerable<MembroDaDiretoria> membros) => _context.MembrosDaDiretoria.RemoveRange(membros);
}
