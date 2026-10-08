using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Queries;

/// <summary>Texto legível de uma origem ("1º do Grupo A", "Vencedor — Quartas, Confronto 1").</summary>
internal sealed class TextoDeReferencia
{
    private readonly Dictionary<Guid, Equipe> _equipes;
    private readonly Dictionary<Guid, Fase> _fases;
    private readonly Dictionary<Guid, Confronto> _confrontos;

    public TextoDeReferencia(IEnumerable<Equipe> equipes, IEnumerable<Fase> fases, IEnumerable<Confronto> confrontos)
    {
        _equipes = equipes.ToDictionary(e => e.Id);
        _fases = fases.ToDictionary(f => f.Id);
        _confrontos = confrontos.ToDictionary(c => c.Id);
    }

    public Equipe? Equipe(Guid? id) => id is not null && _equipes.TryGetValue(id.Value, out var e) ? e : null;

    public string Descrever(ReferenciaEquipe r)
    {
        switch (r.Tipo)
        {
            case TipoReferencia.Equipe:
                return Equipe(r.EquipeId)?.Nome ?? "Equipe removida";

            case TipoReferencia.Colocacao:
                var fase = r.FaseId is not null && _fases.TryGetValue(r.FaseId.Value, out var f) ? f.Nome : "fase removida";
                return r.GrupoOrdem is not null
                    ? $"{r.Posicao}º do Grupo {Grupo.NomeDaOrdem(r.GrupoOrdem.Value)} — {fase}"
                    : $"{r.Posicao}º classificado — {fase}";

            default:
                if (r.ConfrontoOrigemId is null || !_confrontos.TryGetValue(r.ConfrontoOrigemId.Value, out var c))
                    return "Confronto removido";
                var faseDoConfronto = _fases.TryGetValue(c.FaseId, out var fc) ? fc.Nome : "fase";
                return $"{(r.Tipo == TipoReferencia.Vencedor ? "Vencedor" : "Perdedor")} — {faseDoConfronto}, {c.Nome}";
        }
    }
}
