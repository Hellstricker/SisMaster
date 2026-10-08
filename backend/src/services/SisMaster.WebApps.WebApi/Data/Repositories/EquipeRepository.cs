using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class EquipeRepository : IEquipeRepository
{
    private readonly CampeonatoDbContext _context;

    public EquipeRepository(CampeonatoDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Equipe?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Equipes
            .Include(e => e.Atletas)
            .Include(e => e.TemporadaCategoria).ThenInclude(tc => tc.Temporada)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Equipe>> ListarPorTemporadaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.Equipes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(e => e.Atletas).ThenInclude(a => a.InscricaoCategoria).ThenInclude(ic => ic.Inscricao).ThenInclude(i => i.Pessoa)
            .Include(e => e.Atletas).ThenInclude(a => a.InscricaoCategoria).ThenInclude(ic => ic.Inscricao).ThenInclude(i => i.Pagamentos)
            .Where(e => e.TemporadaCategoria.TemporadaId == temporadaId)
            .OrderBy(e => e.Nome)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<InscricaoCategoria>> ListarEfetivadosSemEquipeAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.InscricoesCategorias
            .AsNoTracking()
            .AsSplitQuery()
            .Include(ic => ic.Inscricao).ThenInclude(i => i.Pessoa)
            .Include(ic => ic.Inscricao).ThenInclude(i => i.Pagamentos)
            .Where(ic => ic.Inscricao.TemporadaId == temporadaId
                         && ic.Status == StatusInscricaoCategoria.Efetivada
                         && !_context.Atletas.Any(a => a.InscricaoCategoriaId == ic.Id))
            .OrderBy(ic => ic.Inscricao.Pessoa.Nome)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> ContarPorCategoriaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.Equipes
            .Where(e => e.TemporadaCategoria.TemporadaId == temporadaId)
            .GroupBy(e => e.TemporadaCategoriaId)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

    public async Task<IReadOnlyList<AtletaDoElenco>> ObterElencoAsync(Guid equipeId, CancellationToken ct = default)
    {
        var linhas = await _context.Atletas.AsNoTracking()
            .Where(a => a.EquipeId == equipeId)
            .Select(a => new { a.Id, a.InscricaoCategoria.Inscricao.Pessoa.Nome })
            .ToListAsync(ct);
        return linhas.OrderBy(l => l.Nome).Select(l => new AtletaDoElenco(l.Id, l.Nome)).ToList();
    }

    public async Task<bool> ExisteNomeNaTemporadaAsync(Guid temporadaId, string nome, Guid? ignorarEquipeId, CancellationToken ct = default) =>
        await _context.Equipes.AnyAsync(e =>
            e.TemporadaCategoria.TemporadaId == temporadaId
            && e.Nome.ToLower() == nome.ToLower()
            && (ignorarEquipeId == null || e.Id != ignorarEquipeId), ct);

    public async Task<bool> AtletaExisteParaAsync(Guid inscricaoCategoriaId, CancellationToken ct = default) =>
        await _context.Atletas.AnyAsync(a => a.InscricaoCategoriaId == inscricaoCategoriaId, ct);

    public void Adicionar(Equipe equipe) => _context.Equipes.Add(equipe);
    public void Remover(Equipe equipe) => _context.Equipes.Remove(equipe);
    public void AdicionarAtleta(Atleta atleta) => _context.Atletas.Add(atleta);
    public void RemoverAtleta(Atleta atleta) => _context.Atletas.Remove(atleta);

    public void Dispose() => _context.Dispose();
}
