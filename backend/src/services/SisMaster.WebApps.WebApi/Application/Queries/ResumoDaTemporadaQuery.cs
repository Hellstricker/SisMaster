using Microsoft.EntityFrameworkCore;
using SisMaster.WebApps.WebApi.Data;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Application.Queries;

/// <summary>
/// Números da temporada para a tela de detalhe (G6): fichas e pedidos, uso de cada categoria (impede remoção),
/// jogos em aberto (impedem encerrar) e a situação das cobranças. Somente leitura.
/// </summary>
public class ResumoDaTemporadaQuery
{
    private readonly CampeonatoDbContext _context;

    public ResumoDaTemporadaQuery(CampeonatoDbContext context) => _context = context;

    public async Task<ResumoDaTemporada> ExecutarAsync(Guid temporadaId, CancellationToken ct = default)
    {
        var fichas = await _context.Inscricoes.AsNoTracking()
            .Where(i => i.TemporadaId == temporadaId)
            .Select(i => new
            {
                i.CobrancaGeradaEm,
                i.ValorTotal,
                i.ValorDesconto,
                Pago = i.Pagamentos.Sum(p => (decimal?)p.Valor) ?? 0m,
                TemPedidoAtivo = i.Categorias.Any(c => c.Status != StatusInscricaoCategoria.Recusada),
            })
            .ToListAsync(ct);

        var pedidos = await _context.InscricoesCategorias.AsNoTracking()
            .Where(c => c.Inscricao.TemporadaId == temporadaId)
            .GroupBy(c => new { c.TemporadaCategoriaId, c.Status })
            .Select(g => new { g.Key.TemporadaCategoriaId, g.Key.Status, N = g.Count() })
            .ToListAsync(ct);

        var equipes = await _context.Equipes.AsNoTracking()
            .Where(e => e.TemporadaCategoria.TemporadaId == temporadaId)
            .GroupBy(e => e.TemporadaCategoriaId).Select(g => new { Id = g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.N, ct);

        var fases = await _context.Fases.AsNoTracking()
            .Where(f => f.TemporadaCategoria.TemporadaId == temporadaId)
            .GroupBy(f => f.TemporadaCategoriaId).Select(g => new { Id = g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.N, ct);

        var jogos = await _context.Jogos.AsNoTracking()
            .Where(j => j.TemporadaId == temporadaId)
            .GroupBy(j => j.Status).Select(g => new { Status = g.Key, N = g.Count() })
            .ToListAsync(ct);

        var comCobranca = fichas.Where(f => f.CobrancaGeradaEm != null).ToList();
        var geradaEm = comCobranca.Count == 0 ? (DateTime?)null : await _context.Inscricoes.AsNoTracking()
            .Where(i => i.TemporadaId == temporadaId).MaxAsync(i => i.CobrancaGeradaEm, ct);

        var categorias = pedidos.GroupBy(p => p.TemporadaCategoriaId).ToDictionary(g => g.Key, g => new UsoDeCategoria(
            Pedidos: g.Sum(x => x.N),
            Pendentes: g.Where(x => x.Status == StatusInscricaoCategoria.Pendente).Sum(x => x.N)));

        return new ResumoDaTemporada(
            Fichas: fichas.Count,
            PedidosPendentes: pedidos.Where(p => p.Status == StatusInscricaoCategoria.Pendente).Sum(p => p.N),
            Categorias: categorias,
            EquipesPorCategoria: equipes,
            FasesPorCategoria: fases,
            Equipes: equipes.Values.Sum(),
            Fases: fases.Values.Sum(),
            JogosTotal: jogos.Sum(j => j.N),
            JogosEmAberto: jogos.Where(j => j.Status is StatusJogo.Agendado or StatusJogo.EmAndamento).Sum(j => j.N),
            Cobranca: new SituacaoDaCobranca(
                GeradaEm: geradaEm,
                FichasComCobranca: comCobranca.Count,
                FichasSemCobranca: fichas.Count(f => f.CobrancaGeradaEm == null && f.TemPedidoAtivo),
                Previsto: comCobranca.Sum(f => (f.ValorTotal ?? 0m) - (f.ValorDesconto ?? 0m)),
                Recebido: fichas.Sum(f => f.Pago)));
    }
}

public sealed record UsoDeCategoria(int Pedidos, int Pendentes);

public sealed record SituacaoDaCobranca(DateTime? GeradaEm, int FichasComCobranca, int FichasSemCobranca, decimal Previsto, decimal Recebido);

public sealed record ResumoDaTemporada(
    int Fichas,
    int PedidosPendentes,
    IReadOnlyDictionary<Guid, UsoDeCategoria> Categorias,
    IReadOnlyDictionary<Guid, int> EquipesPorCategoria,
    IReadOnlyDictionary<Guid, int> FasesPorCategoria,
    int Equipes,
    int Fases,
    int JogosTotal,
    int JogosEmAberto,
    SituacaoDaCobranca Cobranca);
