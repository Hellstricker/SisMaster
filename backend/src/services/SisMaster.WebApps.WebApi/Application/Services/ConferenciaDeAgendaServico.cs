using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Services;

/// <summary>
/// Carrega o que a <see cref="ConferenciaDeAgenda"/> precisa e confere uma proposta de agenda: os jogos da temporada que
/// mudariam de data, hora ou local contra todos os jogos agendados e ainda não terminados (o local vale entre temporadas
/// da mesma associação). Usado pelo agendamento (que bloqueia) e pela tela (que mostra os avisos antes de salvar).
/// </summary>
public class ConferenciaDeAgendaServico
{
    private readonly IJogoRepository _jogoRepository;
    private readonly IEquipeRepository _equipeRepository;
    private readonly ILocalRepository _localRepository;

    public ConferenciaDeAgendaServico(IJogoRepository jogoRepository, IEquipeRepository equipeRepository, ILocalRepository localRepository)
    {
        _jogoRepository = jogoRepository;
        _equipeRepository = equipeRepository;
        _localRepository = localRepository;
    }

    public async Task<ResultadoDaConferencia> ConferirAsync(
        Guid temporadaId, IReadOnlyList<AgendaProposta> propostas, CancellationToken ct = default)
    {
        if (propostas.Count == 0) return ResultadoDaConferencia.Vazio;

        var daTemporada = await _jogoRepository.ListarPorTemporadaAsync(temporadaId, ct);
        var porId = daTemporada.ToDictionary(j => j.Id);
        var ids = propostas.Select(p => p.JogoId).ToHashSet();

        var locais = new Dictionary<Guid, string>();
        foreach (var localId in propostas.Where(p => p.LocalId is not null).Select(p => p.LocalId!.Value).Distinct())
        {
            var local = await _localRepository.ObterPorIdAsync(localId, ct);
            if (local is not null) locais[localId] = local.Nome;
        }

        var propostos = new List<JogoNaAgenda>();
        foreach (var p in propostas)
        {
            if (!porId.TryGetValue(p.JogoId, out var jogo)) continue;
            var nomeLocal = p.LocalId is { } l ? locais.GetValueOrDefault(l) : null;
            propostos.Add(new JogoNaAgenda(jogo.Id, jogo.TemporadaId, jogo.Numero, jogo.Casa.EquipeId, jogo.Visitante.EquipeId, p.Data, p.Hora, p.LocalId, nomeLocal));
        }

        var pendentes = await _jogoRepository.ListarAgendadosPendentesAsync(ct);
        var existentes = pendentes.Where(j => !ids.Contains(j.Id))
            .Select(j => new JogoNaAgenda(j.Id, j.TemporadaId, j.Numero, j.Casa.EquipeId, j.Visitante.EquipeId, j.Data, j.Hora, j.LocalId, j.Local?.Nome))
            .ToList();

        var elencos = await _equipeRepository.ObterElencosAsync(temporadaId, ct);
        string Descrever(JogoNaAgenda j)
        {
            if (j.TemporadaId != temporadaId) return $"#{j.Numero}, de outra temporada";
            string Lado(Guid? e) => e is { } id ? elencos.NomesDasEquipes.GetValueOrDefault(id, "?") : "a definir";
            return $"#{j.Numero} {Lado(j.CasaEquipeId)} × {Lado(j.VisitanteEquipeId)}";
        }

        return ConferenciaDeAgenda.Conferir(propostos, existentes, elencos, Descrever);
    }
}
