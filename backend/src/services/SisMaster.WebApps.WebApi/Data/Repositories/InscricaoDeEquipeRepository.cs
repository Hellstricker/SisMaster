using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Torneio;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class InscricaoDeEquipeRepository : IInscricaoDeEquipeRepository
{
    private readonly CampeonatoDbContext _context;

    public InscricaoDeEquipeRepository(CampeonatoDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    public async Task<InscricaoDeEquipe?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.InscricoesDeEquipe.Include(i => i.Convites).FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<InscricaoDeEquipe?> ObterPorCodigoDeEnvioAsync(Guid codigo, CancellationToken ct = default) =>
        await _context.InscricoesDeEquipe.Include(i => i.Convites).FirstOrDefaultAsync(i => i.CodigoDeEnvio == codigo, ct);

    public async Task<InscricaoDeEquipe?> ObterPorCodigoDoConviteAsync(string codigo, CancellationToken ct = default)
    {
        var hash = InscricaoDeEquipe.Hash(codigo);
        return await _context.InscricoesDeEquipe.Include(i => i.Convites)
            .FirstOrDefaultAsync(i => i.Convites.Any(c => c.CodigoHash == hash), ct);
    }

    public async Task<InscricaoDeEquipe?> ObterPorFichaAsync(Guid inscricaoId, CancellationToken ct = default) =>
        await _context.InscricoesDeEquipe.Include(i => i.Convites)
            .FirstOrDefaultAsync(i => i.Convites.Any(c => c.InscricaoId == inscricaoId), ct);

    public async Task<IReadOnlyList<InscricaoDeEquipe>> ListarDaContaAsync(string contaId, CancellationToken ct = default) =>
        await _context.InscricoesDeEquipe.AsNoTracking().Include(i => i.Convites)
            .Where(i => i.ContaId == contaId).OrderByDescending(i => i.DataEnvio).ToListAsync(ct);

    public async Task<IReadOnlyList<InscricaoDeEquipe>> ListarDaTemporadaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.InscricoesDeEquipe.AsNoTracking().Include(i => i.Convites)
            .Where(i => i.TemporadaId == temporadaId).OrderByDescending(i => i.DataEnvio).ToListAsync(ct);

    public async Task<int> ContarProtocolosAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.InscricoesDeEquipe.CountAsync(i => i.TemporadaId == temporadaId, ct);

    public async Task<bool> ExisteNomeAsync(Guid temporadaId, string nome, CancellationToken ct = default)
    {
        var n = nome.Trim().ToLower();
        return await _context.InscricoesDeEquipe.AnyAsync(i => i.TemporadaId == temporadaId
            && i.Status != StatusInscricaoDeEquipe.Recusada && i.Nome.ToLower() == n, ct);
    }

    public async Task<IReadOnlyList<string>> CpfsJaConvidadosAsync(Guid temporadaCategoriaId, IReadOnlyCollection<string> cpfs, CancellationToken ct = default) =>
        await _context.InscricoesDeEquipe.AsNoTracking()
            .Where(i => i.TemporadaCategoriaId == temporadaCategoriaId && i.Status != StatusInscricaoDeEquipe.Recusada)
            .SelectMany(i => i.Convites)
            .Where(c => (c.Status == StatusDoConvite.Enviado || c.Status == StatusDoConvite.Confirmado) && c.Cpf != null && cpfs.Contains(c.Cpf))
            .Select(c => c.Cpf!)
            .ToListAsync(ct);

    public void Adicionar(InscricaoDeEquipe inscricao) => _context.InscricoesDeEquipe.Add(inscricao);

    public void AdicionarConvite(ConviteDeAtleta convite) => _context.ConvitesDeAtleta.Add(convite);

    public void Dispose() => _context.Dispose();
}
