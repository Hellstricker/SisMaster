using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class JogoRepository : IJogoRepository
{
    private readonly CampeonatoDbContext _context;

    public JogoRepository(CampeonatoDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Jogo?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Jogos.Include(j => j.Casa).Include(j => j.Visitante).FirstOrDefaultAsync(j => j.Id == id, ct);

    public async Task<List<Confronto>> ListarConfrontosDaFaseAsync(Guid faseId, CancellationToken ct = default) =>
        await _context.Confrontos.Where(c => c.FaseId == faseId).OrderBy(c => c.Numero).ToListAsync(ct);

    public async Task<IReadOnlyList<Confronto>> ListarConfrontosDaCategoriaAsync(Guid temporadaCategoriaId, CancellationToken ct = default) =>
        await _context.Confrontos.AsNoTracking()
            .Where(c => _context.Fases.Any(f => f.Id == c.FaseId && f.TemporadaCategoriaId == temporadaCategoriaId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Confronto>> ListarConfrontosDaTemporadaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.Confrontos.AsNoTracking()
            .Where(c => _context.Fases.Any(f => f.Id == c.FaseId && f.TemporadaCategoria.TemporadaId == temporadaId))
            .ToListAsync(ct);

    public async Task<bool> ExisteReferenciaAFaseAsync(Guid faseId, CancellationToken ct = default) =>
        await _context.Confrontos.AnyAsync(c => c.OrigemA.FaseId == faseId || c.OrigemB.FaseId == faseId, ct);

    public async Task<bool> ExisteReferenciaAosConfrontosAsync(IReadOnlyCollection<Guid> confrontoIds, CancellationToken ct = default) =>
        confrontoIds.Count > 0 &&
        await _context.Confrontos.AnyAsync(c =>
            (c.OrigemA.ConfrontoOrigemId != null && confrontoIds.Contains(c.OrigemA.ConfrontoOrigemId.Value)) ||
            (c.OrigemB.ConfrontoOrigemId != null && confrontoIds.Contains(c.OrigemB.ConfrontoOrigemId.Value)), ct);

    public async Task<IReadOnlyList<SorteioDeDesempate>> ListarSorteiosDaTemporadaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.SorteiosDeDesempate.AsNoTracking()
            .Where(x => _context.Fases.Any(f => f.Id == x.FaseId && f.TemporadaCategoria.TemporadaId == temporadaId)).ToListAsync(ct);

    public async Task<IReadOnlyList<SorteioDeDesempate>> ListarSorteiosDaFaseAsync(Guid faseId, CancellationToken ct = default) =>
        await _context.SorteiosDeDesempate.AsNoTracking().Where(x => x.FaseId == faseId).ToListAsync(ct);

    public async Task<SorteioDeDesempate?> ObterSorteioAsync(Guid faseId, Guid? grupoId, CancellationToken ct = default) =>
        await _context.SorteiosDeDesempate.FirstOrDefaultAsync(x => x.FaseId == faseId && x.GrupoId == grupoId, ct);

    public void AdicionarSorteio(SorteioDeDesempate sorteio) => _context.SorteiosDeDesempate.Add(sorteio);

    public async Task<bool> ExisteVagaResolvidaPelaColocacaoAsync(Guid faseId, CancellationToken ct = default) =>
        await _context.FaseEquipes.AnyAsync(v => v.EquipeId != null && v.Origem.Tipo == TipoReferencia.Colocacao && v.Origem.FaseId == faseId, ct);

    public async Task<IReadOnlyList<Jogo>> ListarAgendadosPendentesAsync(CancellationToken ct = default) =>
        await _context.Jogos.AsNoTracking()
            .Include(j => j.Local).Include(j => j.Casa).Include(j => j.Visitante)
            .Where(j => j.Data != null && (j.Status == StatusJogo.Agendado || j.Status == StatusJogo.EmAndamento)
                && _context.Temporadas.Any(t => t.Id == j.TemporadaId && t.Status != Domain.Associacao.StatusTemporada.Encerrada))
            .OrderBy(j => j.Data).ThenBy(j => j.Hora).ThenBy(j => j.Numero)
            .ToListAsync(ct);

    public async Task<int> ContarJogosNoLocalAsync(Guid localId, CancellationToken ct = default) =>
        await _context.Jogos.CountAsync(j => j.LocalId == localId, ct);

    public async Task<bool> ExistemJogosNaTemporadaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.Jogos.AnyAsync(j => j.TemporadaId == temporadaId, ct);

    public async Task<IReadOnlyDictionary<Guid, List<StatusJogo>>> StatusPorFaseAsync(Guid temporadaId, CancellationToken ct = default)
    {
        var linhas = await _context.Jogos.AsNoTracking()
            .Where(j => j.TemporadaId == temporadaId)
            .Select(j => new { j.FaseId, j.Status })
            .ToListAsync(ct);
        return linhas.GroupBy(l => l.FaseId).ToDictionary(g => g.Key, g => g.Select(l => l.Status).ToList());
    }

    public async Task<TabelaGerada> ObterTabelaDaFaseAsync(Guid faseId, CancellationToken ct = default)
    {
        var tabela = new TabelaGerada();
        tabela.Grupos.AddRange(await _context.Grupos.AsNoTracking().Where(g => g.FaseId == faseId).OrderBy(g => g.Ordem).ToListAsync(ct));
        tabela.Vagas.AddRange(await _context.FaseEquipes.AsNoTracking().Include(v => v.Equipe).Where(v => v.FaseId == faseId).OrderBy(v => v.Posicao).ToListAsync(ct));
        tabela.Jogos.AddRange(await _context.Jogos.AsNoTracking()
            .Include(j => j.Local).Include(j => j.Casa).Include(j => j.Visitante)
            .Where(j => j.FaseId == faseId).OrderBy(j => j.Numero).ToListAsync(ct));
        return tabela;
    }

    public async Task<IReadOnlyList<Jogo>> ListarPorTemporadaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.Jogos.AsNoTracking()
            .Include(j => j.Local).Include(j => j.Casa).Include(j => j.Visitante)
            .Where(j => j.TemporadaId == temporadaId).OrderBy(j => j.Numero).ToListAsync(ct);

    public async Task<List<Jogo>> ListarPorIdsAsync(Guid temporadaId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        await _context.Jogos.Where(j => j.TemporadaId == temporadaId && ids.Contains(j.Id)).OrderBy(j => j.Numero).ToListAsync(ct);

    public async Task<DadosDaTemporada> CarregarParaAvancoAsync(Guid temporadaId, CancellationToken ct = default)
    {
        var fases = await _context.Fases.Include(f => f.TemporadaCategoria).ThenInclude(c => c.Temporada)
            .Where(f => f.TemporadaCategoria.TemporadaId == temporadaId).ToListAsync(ct);
        var faseIds = fases.Select(f => f.Id).ToList();
        var grupos = await _context.Grupos.Where(g => faseIds.Contains(g.FaseId)).ToListAsync(ct);
        var vagas = await _context.FaseEquipes.Where(v => faseIds.Contains(v.FaseId)).ToListAsync(ct);
        var confrontos = await _context.Confrontos.Where(c => faseIds.Contains(c.FaseId)).ToListAsync(ct);
        var jogos = await _context.Jogos.Include(j => j.Casa).Include(j => j.Visitante).Where(j => j.TemporadaId == temporadaId).ToListAsync(ct);
        var sorteios = await _context.SorteiosDeDesempate.Where(x => faseIds.Contains(x.FaseId)).ToListAsync(ct);
        return new DadosDaTemporada(fases, grupos, vagas, confrontos, jogos, sorteios);
    }

    public void AdicionarConfronto(Confronto confronto) => _context.Confrontos.Add(confronto);
    public void RemoverConfronto(Confronto confronto) => _context.Confrontos.Remove(confronto);

    public void AdicionarTabela(TabelaGerada tabela)
    {
        _context.Grupos.AddRange(tabela.Grupos);
        _context.FaseEquipes.AddRange(tabela.Vagas);
        _context.Jogos.AddRange(tabela.Jogos);
    }

    public void Dispose() => _context.Dispose();
}

public class LocalRepository : ILocalRepository
{
    private readonly CampeonatoDbContext _context;

    public LocalRepository(CampeonatoDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Local?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Locais.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<Local>> ListarPorAssociacaoAsync(Guid associacaoId, CancellationToken ct = default) =>
        await _context.Locais.AsNoTracking().Where(l => l.AssociacaoId == associacaoId)
            .OrderBy(l => l.Cidade).ThenBy(l => l.Nome).ToListAsync(ct);

    public async Task<bool> ExisteAsync(Guid associacaoId, string nome, string cidade, Guid? ignorarId, CancellationToken ct = default) =>
        await _context.Locais.AnyAsync(l => l.AssociacaoId == associacaoId && l.Nome == nome && l.Cidade == cidade && l.Id != ignorarId, ct);

    public void Remover(Local local) => _context.Locais.Remove(local);

    public async Task<IReadOnlyDictionary<Guid, int>> ContarUsosAsync(Guid associacaoId, CancellationToken ct = default) =>
        await _context.Jogos.Where(j => j.LocalId != null && _context.Locais.Any(l => l.Id == j.LocalId && l.AssociacaoId == associacaoId))
            .GroupBy(j => j.LocalId!.Value).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);

    public void Adicionar(Local local) => _context.Locais.Add(local);

    public void Dispose() => _context.Dispose();
}
