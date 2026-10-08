using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Queries;

/// <summary>
/// A tabela de jogos da temporada inteira (todas as categorias misturadas), na ordem da numeração. Os filtros e o
/// agrupamento por data/local ficam na tela: uma temporada tem poucas centenas de jogos.
/// </summary>
public class JogosDaTemporadaQuery
{
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly IFaseRepository _faseRepository;
    private readonly IJogoRepository _jogoRepository;
    private readonly IEquipeRepository _equipeRepository;

    public JogosDaTemporadaQuery(ITemporadaRepository temporadaRepository, IFaseRepository faseRepository,
        IJogoRepository jogoRepository, IEquipeRepository equipeRepository)
    {
        _temporadaRepository = temporadaRepository;
        _faseRepository = faseRepository;
        _jogoRepository = jogoRepository;
        _equipeRepository = equipeRepository;
    }

    public async Task<object?> ExecutarAsync(Guid temporadaId, CancellationToken ct)
    {
        var temporada = await _temporadaRepository.ObterPorIdAsync(temporadaId, ct);
        if (temporada is null) return null;

        var fases = await _faseRepository.ListarPorTemporadaAsync(temporadaId, ct);
        var confrontos = await _jogoRepository.ListarConfrontosDaTemporadaAsync(temporadaId, ct);
        var equipes = await _equipeRepository.ListarPorTemporadaAsync(temporadaId, ct);
        var jogos = await _jogoRepository.ListarPorTemporadaAsync(temporadaId, ct);

        var texto = new TextoDeReferencia(equipes, fases, confrontos);
        var faseDe = fases.ToDictionary(f => f.Id);
        var categorias = temporada.Categorias.ToDictionary(c => c.Id);
        var confrontoDe = confrontos.ToDictionary(c => c.Id);
        var gruposPorFase = new Dictionary<Guid, Dictionary<Guid, string>>();

        object Lado(FaseEquipe v)
        {
            var equipe = texto.Equipe(v.EquipeId);
            return new { Texto = equipe?.Nome ?? texto.Descrever(v.Origem), Definida = equipe is not null, EquipeId = equipe?.Id };
        }

        return new
        {
            temporada.Id,
            temporada.Ano,
            temporada.Status,
            AssociacaoId = temporada.Campeonato.AssociacaoId,
            TabelaGerada = temporada.TabelaJogosGerada,
            Categorias = temporada.Categorias.OrderBy(c => c.Nome).Select(c => new { c.Id, c.Nome }),
            Fases = fases.Select(f => new { f.Id, f.Nome, f.Ordem, f.TemporadaCategoriaId }),
            Jogos = jogos.Select(j =>
            {
                var fase = faseDe[j.FaseId];
                var confronto = j.ConfrontoId is null ? null : confrontoDe.GetValueOrDefault(j.ConfrontoId.Value);
                return new
                {
                    j.Id,
                    j.Numero,
                    j.Rodada,
                    j.JogoDaSerie,
                    j.Opcional,
                    Categoria = new { Id = fase.TemporadaCategoriaId, Nome = categorias[fase.TemporadaCategoriaId].Nome },
                    Fase = new { fase.Id, fase.Nome, fase.Tipo },
                    // "R3" em pontos corridos/grupos; "Final · J2" no mata-mata.
                    Etiqueta = confronto is not null ? $"{confronto.Nome} · J{j.JogoDaSerie}" : $"R{j.Rodada}",
                    Casa = Lado(j.Casa),
                    Visitante = Lado(j.Visitante),
                    j.Data,
                    j.Hora,
                    Local = j.Local is null ? null : new { j.Local.Id, j.Local.Nome, j.Local.Cidade, j.Local.Estado },
                    j.Status,
                    j.PlacarCasa,
                    j.PlacarVisitante
                };
            })
        };
    }
}
