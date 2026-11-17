using Microsoft.EntityFrameworkCore;
using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Data.Repositories;

public class InscricaoRepository : IInscricaoRepository
{
    private readonly CampeonatoDbContext _context;

    public InscricaoRepository(CampeonatoDbContext context) => _context = context;

    public IUnitOfWork UnitOfWork => _context;

    private IQueryable<Inscricao> FichaCompleta() =>
        _context.Inscricoes
            .Include(i => i.Pessoa)
            .Include(i => i.Pagamentos)
            .Include(i => i.Categorias).ThenInclude(c => c.TemporadaCategoria).ThenInclude(tc => tc.Temporada);

    public async Task<Inscricao?> ObterPorIdAsync(Guid inscricaoId, CancellationToken ct = default) =>
        await FichaCompleta().FirstOrDefaultAsync(i => i.Id == inscricaoId, ct);

    public async Task<Inscricao?> ObterPorCategoriaIdAsync(Guid inscricaoCategoriaId, CancellationToken ct = default) =>
        await FichaCompleta().FirstOrDefaultAsync(i => i.Categorias.Any(c => c.Id == inscricaoCategoriaId), ct);

    public async Task<IReadOnlyList<Inscricao>> ListarPorTemporadaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await FichaCompleta().Where(i => i.TemporadaId == temporadaId).ToListAsync(ct);

    public async Task<IReadOnlyList<Inscricao>> ListarFichasPorTemporadaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.Inscricoes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(i => i.Pessoa)
            .Include(i => i.Pagamentos)
            .Include(i => i.Categorias).ThenInclude(c => c.TemporadaCategoria)
            .Where(i => i.TemporadaId == temporadaId)
            .OrderByDescending(i => i.DataEnvio)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<InscricaoCategoria>> ListarPorTemporadaCategoriaAsync(Guid temporadaCategoriaId, CancellationToken ct = default) =>
        await _context.InscricoesCategorias
            .AsNoTracking()
            .Include(c => c.Inscricao).ThenInclude(i => i.Pessoa)
            .Include(c => c.Inscricao).ThenInclude(i => i.Pagamentos)
            .Include(c => c.TemporadaCategoria).ThenInclude(tc => tc.Temporada)
            .Where(c => c.TemporadaCategoriaId == temporadaCategoriaId)
            .OrderByDescending(c => c.Inscricao.DataEnvio)
            .ToListAsync(ct);

    public async Task<Inscricao?> ObterPorCodigoDeEnvioAsync(Guid codigo, CancellationToken ct = default) =>
        await _context.Inscricoes.Include(i => i.Categorias).FirstOrDefaultAsync(i => i.CodigoDeEnvio == codigo, ct);

    public async Task<Inscricao?> ObterUltimaDaPessoaAsync(Guid pessoaId, CancellationToken ct = default) =>
        await _context.Inscricoes.AsNoTracking().Where(i => i.PessoaId == pessoaId).OrderByDescending(i => i.DataEnvio).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Inscricao>> ListarDaPessoaAsync(Guid pessoaId, CancellationToken ct = default) =>
        await _context.Inscricoes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(i => i.Pagamentos)
            .Include(i => i.Categorias).ThenInclude(c => c.TemporadaCategoria).ThenInclude(tc => tc.Temporada).ThenInclude(t => t.Campeonato).ThenInclude(c => c.Associacao)
            .Where(i => i.PessoaId == pessoaId)
            .OrderByDescending(i => i.DataEnvio)
            .ToListAsync(ct);

    public async Task<Inscricao?> ObterDaPessoaAsync(Guid inscricaoId, Guid pessoaId, CancellationToken ct = default) =>
        await _context.Inscricoes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(i => i.Pagamentos)
            .Include(i => i.Categorias).ThenInclude(c => c.TemporadaCategoria).ThenInclude(tc => tc.Temporada).ThenInclude(t => t.Campeonato).ThenInclude(c => c.Associacao)
            .FirstOrDefaultAsync(i => i.Id == inscricaoId && i.PessoaId == pessoaId, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> ListarEquipesDosPedidosAsync(IReadOnlyCollection<Guid> inscricaoCategoriaIds, CancellationToken ct = default) =>
        await _context.Atletas.AsNoTracking()
            .Where(a => inscricaoCategoriaIds.Contains(a.InscricaoCategoriaId))
            .Join(_context.Equipes, a => a.EquipeId, e => e.Id, (a, e) => new { a.InscricaoCategoriaId, e.Nome })
            .ToDictionaryAsync(x => x.InscricaoCategoriaId, x => x.Nome, ct);

    public async Task<IReadOnlyList<Inscricao>> ListarParaUniaoAsync(Guid pessoaId, CancellationToken ct = default) =>
        await _context.Inscricoes.Include(i => i.Categorias).Where(i => i.PessoaId == pessoaId).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> ListarCategoriasAtivasDaPessoaAsync(Guid pessoaId, Guid temporadaId, CancellationToken ct = default) =>
        (await _context.InscricoesCategorias.AsNoTracking()
            .Where(c => c.Inscricao.PessoaId == pessoaId && c.Inscricao.TemporadaId == temporadaId && c.Status != StatusInscricaoCategoria.Recusada)
            .Select(c => new { c.TemporadaCategoriaId, c.Inscricao.Protocolo })
            .ToListAsync(ct))
        .GroupBy(x => x.TemporadaCategoriaId)
        .ToDictionary(g => g.Key, g => g.First().Protocolo ?? "—");

    public async Task<int> ContarProtocolosAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.Inscricoes.CountAsync(i => i.TemporadaId == temporadaId && i.Protocolo != null, ct);

    public async Task<bool> ExisteAtivaAsync(Guid pessoaId, Guid temporadaCategoriaId, CancellationToken ct = default) =>
        await _context.InscricoesCategorias.AnyAsync(c =>
            c.TemporadaCategoriaId == temporadaCategoriaId
            && c.Inscricao.PessoaId == pessoaId
            && c.Status != StatusInscricaoCategoria.Recusada, ct);

    public async Task<bool> ExisteAtivaPorCpfAsync(string cpfSomenteDigitos, Guid temporadaCategoriaId, CancellationToken ct = default) =>
        await _context.InscricoesCategorias.AnyAsync(c =>
            c.TemporadaCategoriaId == temporadaCategoriaId
            && c.Inscricao.Pessoa.Cpf.Numero == cpfSomenteDigitos
            && c.Status != StatusInscricaoCategoria.Recusada, ct);

    public async Task<bool> ExisteCobrancaGeradaAsync(Guid temporadaId, CancellationToken ct = default) =>
        await _context.Inscricoes.AnyAsync(i => i.TemporadaId == temporadaId && i.CobrancaGeradaEm != null, ct);

    public void Adicionar(Inscricao inscricao) => _context.Inscricoes.Add(inscricao);

    // Filho novo de agregado já carregado: Add explícito (ver nota sobre DbSet.Update/Modified).
    public void AdicionarPagamento(PagamentoInscricao pagamento) => _context.PagamentosInscricao.Add(pagamento);

    public void Dispose() => _context.Dispose();
}
