using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Participantes;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

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

    public async Task<ElencosDaTemporada> ObterElencosAsync(Guid temporadaId, CancellationToken ct = default)
    {
        var nomes = await _context.Equipes.AsNoTracking()
            .Where(e => e.TemporadaCategoria.TemporadaId == temporadaId)
            .ToDictionaryAsync(e => e.Id, e => e.Nome, ct);
        var ids = nomes.Keys.ToList();
        var linhas = await _context.Atletas.AsNoTracking()
            .Where(a => ids.Contains(a.EquipeId))
            .Select(a => new { a.EquipeId, a.InscricaoCategoria.Inscricao.PessoaId, a.InscricaoCategoria.Inscricao.Pessoa.Nome })
            .ToListAsync(ct);
        var pessoas = linhas.GroupBy(l => l.EquipeId).ToDictionary(
            g => g.Key, g => (IReadOnlyList<PessoaDoElenco>)g.Select(l => new PessoaDoElenco(l.PessoaId, l.Nome)).ToList());
        return new ElencosDaTemporada(nomes, pessoas);
    }

    public async Task<bool> ExisteNomeNaTemporadaAsync(Guid temporadaId, string nome, Guid? ignorarEquipeId, CancellationToken ct = default) =>
        await _context.Equipes.AnyAsync(e =>
            e.TemporadaCategoria.TemporadaId == temporadaId
            && e.Nome.ToLower() == nome.ToLower()
            && (ignorarEquipeId == null || e.Id != ignorarEquipeId), ct);

    public async Task<bool> AtletaExisteParaAsync(Guid inscricaoCategoriaId, CancellationToken ct = default) =>
        await _context.Atletas.AnyAsync(a => a.InscricaoCategoriaId == inscricaoCategoriaId, ct);

    public async Task<bool> AtletaApareceEmSumulaAsync(Guid atletaId, CancellationToken ct = default) =>
        await _context.SumulaJogadores.AnyAsync(j => j.AtletaId == atletaId, ct);

    public async Task<bool> AtletaEstaEmSumulaEmAbertoAsync(Guid atletaId, CancellationToken ct = default) =>
        await (from j in _context.SumulaJogadores
               join t in _context.SumulaTimes on j.TimeId equals t.Id
               join s in _context.Sumulas on t.SumulaId equals s.Id
               where j.AtletaId == atletaId && s.Status != StatusSumula.Encerrada
               select j.Id).AnyAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ParticipacaoDoAtleta>>> ObterParticipacoesDaTemporadaAsync(Guid temporadaId, CancellationToken ct = default)
    {
        var linhas = await (from j in _context.SumulaJogadores.AsNoTracking()
                            join t in _context.SumulaTimes on j.TimeId equals t.Id
                            join s in _context.Sumulas on t.SumulaId equals s.Id
                            join a in _context.Atletas on j.AtletaId equals a.Id
                            where a.InscricaoCategoria.Inscricao.TemporadaId == temporadaId && s.Status != StatusSumula.EmPreparacao
                            group j by new { j.AtletaId, t.EquipeId, t.Nome } into g
                            select new { g.Key.AtletaId, g.Key.EquipeId, g.Key.Nome, Jogos = g.Count() })
            .ToListAsync(ct);

        return linhas.GroupBy(l => l.AtletaId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ParticipacaoDoAtleta>)g.Select(l => new ParticipacaoDoAtleta(l.EquipeId, l.Nome, l.Jogos)).ToList());
    }

    public void Adicionar(Equipe equipe) => _context.Equipes.Add(equipe);
    public void Remover(Equipe equipe) => _context.Equipes.Remove(equipe);
    public void AdicionarAtleta(Atleta atleta) => _context.Atletas.Add(atleta);
    public void RemoverAtleta(Atleta atleta) => _context.Atletas.Remove(atleta);

    public void Dispose() => _context.Dispose();
}
