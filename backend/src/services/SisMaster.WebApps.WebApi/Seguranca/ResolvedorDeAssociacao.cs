using Microsoft.EntityFrameworkCore;
using SisMaster.WebApps.WebApi.Data;

namespace SisMaster.WebApps.WebApi.Seguranca;

/// <summary>Descobre a qual associação pertence um recurso, a partir do id dele. Nulo = o recurso não existe.</summary>
public interface IResolvedorDeAssociacao
{
    Task<Guid?> AssociacaoDoAsync(Recurso recurso, Guid id, CancellationToken ct = default);
}

public sealed class ResolvedorDeAssociacao(CampeonatoDbContext db) : IResolvedorDeAssociacao
{
    public async Task<Guid?> AssociacaoDoAsync(Recurso recurso, Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty) return null;

        switch (recurso)
        {
            case Recurso.Associacao:
                return await db.Associacoes.AsNoTracking().Where(a => a.Id == id).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);

            case Recurso.Campeonato:
                return await db.Campeonatos.AsNoTracking().Where(c => c.Id == id).Select(c => (Guid?)c.AssociacaoId).FirstOrDefaultAsync(ct);

            case Recurso.Categoria:
                return await db.Categorias.AsNoTracking().Where(c => c.Id == id).Select(c => (Guid?)c.AssociacaoId).FirstOrDefaultAsync(ct);

            case Recurso.Local:
                return await db.Locais.AsNoTracking().Where(l => l.Id == id).Select(l => (Guid?)l.AssociacaoId).FirstOrDefaultAsync(ct);

            case Recurso.Temporada:
                return await DaTemporada(db.Temporadas.Where(t => t.Id == id).Select(t => t.Id), ct);

            case Recurso.TemporadaCategoria:
                return await DaTemporada(db.TemporadaCategorias.Where(x => x.Id == id).Select(x => x.TemporadaId), ct);

            case Recurso.Inscricao:
                return await DaTemporada(db.Inscricoes.Where(x => x.Id == id).Select(x => x.TemporadaId), ct);

            case Recurso.InscricaoDeEquipe:
                return await DaTemporada(db.InscricoesDeEquipe.Where(x => x.Id == id).Select(x => x.TemporadaId), ct);

            case Recurso.InscricaoCategoria:
                return await DaTemporada(
                    db.TemporadaCategorias
                        .Where(tc => db.InscricoesCategorias.Where(ic => ic.Id == id).Select(ic => ic.TemporadaCategoriaId).Contains(tc.Id))
                        .Select(tc => tc.TemporadaId), ct);

            case Recurso.Equipe:
                return await DaTemporada(
                    db.TemporadaCategorias
                        .Where(tc => db.Equipes.Where(e => e.Id == id).Select(e => e.TemporadaCategoriaId).Contains(tc.Id))
                        .Select(tc => tc.TemporadaId), ct);

            case Recurso.Fase:
                return await DaTemporada(
                    db.TemporadaCategorias
                        .Where(tc => db.Fases.Where(f => f.Id == id).Select(f => f.TemporadaCategoriaId).Contains(tc.Id))
                        .Select(tc => tc.TemporadaId), ct);

            case Recurso.Jogo:
                return await DaTemporada(db.Jogos.Where(j => j.Id == id).Select(j => j.TemporadaId), ct);

            case Recurso.Sumula:
                return await DaTemporada(
                    db.Jogos
                        .Where(j => db.Sumulas.Where(s => s.Id == id).Select(s => s.JogoId).Contains(j.Id))
                        .Select(j => j.TemporadaId), ct);

            default:
                return null;
        }
    }

    /// <summary>temporada -> campeonato -> associação.</summary>
    private Task<Guid?> DaTemporada(IQueryable<Guid> temporadaIds, CancellationToken ct) =>
        (from t in db.Temporadas.AsNoTracking()
         where temporadaIds.Contains(t.Id)
         join c in db.Campeonatos.AsNoTracking() on t.CampeonatoId equals c.Id
         select (Guid?)c.AssociacaoId).FirstOrDefaultAsync(ct);
}
