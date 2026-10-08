using SisMaster.WebApps.WebApi.Domain.Associacao;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Application.Queries;

/// <summary>
/// Jogos que o árbitro pode atender: já agendados (com data) e ainda não terminados, de todas as temporadas em aberto,
/// com a situação da súmula de cada um. A tela decide o que é "hoje", "atrasado" e "próximos dias".
/// </summary>
public class JogosDoArbitroQuery
{
    private readonly IJogoRepository _jogoRepository;
    private readonly ITemporadaRepository _temporadaRepository;
    private readonly IFaseRepository _faseRepository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly ISumulaRepository _sumulaRepository;

    public JogosDoArbitroQuery(IJogoRepository jogoRepository, ITemporadaRepository temporadaRepository, IFaseRepository faseRepository,
        IEquipeRepository equipeRepository, ISumulaRepository sumulaRepository)
    {
        _jogoRepository = jogoRepository;
        _temporadaRepository = temporadaRepository;
        _faseRepository = faseRepository;
        _equipeRepository = equipeRepository;
        _sumulaRepository = sumulaRepository;
    }

    public async Task<object> ExecutarAsync(CancellationToken ct)
    {
        var jogos = await _jogoRepository.ListarAgendadosPendentesAsync(ct);
        var sumulas = jogos.Count == 0
            ? new Dictionary<Guid, Sumula>()
            : (await _sumulaRepository.ListarPorJogosAsync(jogos.Select(j => j.Id).ToList(), ct)).ToDictionary(s => s.JogoId);

        var itens = new List<(DateOnly? Data, TimeOnly? Hora, int Numero, object Item)>();
        foreach (var grupo in jogos.GroupBy(j => j.TemporadaId))
        {
            var temporada = await _temporadaRepository.ObterPorIdAsync(grupo.Key, ct);
            if (temporada is null) continue;

            var fases = await _faseRepository.ListarPorTemporadaAsync(temporada.Id, ct);
            var confrontos = await _jogoRepository.ListarConfrontosDaTemporadaAsync(temporada.Id, ct);
            var equipes = await _equipeRepository.ListarPorTemporadaAsync(temporada.Id, ct);
            var texto = new TextoDeReferencia(equipes, fases, confrontos);
            var faseDe = fases.ToDictionary(f => f.Id);
            var categorias = temporada.Categorias.ToDictionary(c => c.Id);
            var confrontoDe = confrontos.ToDictionary(c => c.Id);

            object Lado(FaseEquipe v)
            {
                var equipe = texto.Equipe(v.EquipeId);
                return new { Texto = equipe?.Nome ?? texto.Descrever(v.Origem), Definida = equipe is not null };
            }

            foreach (var j in grupo)
            {
                var fase = faseDe[j.FaseId];
                var confronto = j.ConfrontoId is null ? null : confrontoDe.GetValueOrDefault(j.ConfrontoId.Value);
                var sumula = sumulas.GetValueOrDefault(j.Id);
                itens.Add((j.Data, j.Hora, j.Numero, new
                {
                    j.Id,
                    j.Numero,
                    Temporada = new { temporada.Id, temporada.Ano },
                    Categoria = new { Id = fase.TemporadaCategoriaId, Nome = categorias[fase.TemporadaCategoriaId].Nome },
                    Fase = new { fase.Id, fase.Nome },
                    Etiqueta = confronto is not null ? $"{confronto.Nome} · J{j.JogoDaSerie}" : $"R{j.Rodada}",
                    Casa = Lado(j.Casa),
                    Visitante = Lado(j.Visitante),
                    PodePrepararSumula = j.Casa.EquipeId is not null && j.Visitante.EquipeId is not null,
                    j.Data,
                    j.Hora,
                    Local = j.Local is null ? null : new { j.Local.Id, j.Local.Nome, j.Local.Cidade, j.Local.Estado },
                    j.Status,
                    Sumula = sumula is null ? null : new { sumula.Id, Status = sumula.Status.ToString() }
                }));
            }
        }

        return new { Jogos = itens.OrderBy(i => i.Data).ThenBy(i => i.Hora).ThenBy(i => i.Numero).Select(i => i.Item).ToList() };
    }
}
