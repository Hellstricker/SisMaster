using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Application.Queries;

/// <summary>
/// Quadra, banco e rodízio de cada time de uma súmula (mesário de estatísticas): quem está em quadra, a situação de cada
/// jogador nos períodos normais já encerrados e a bonificação prevista. Tudo derivado das substituições.
/// </summary>
public class RodizioDaSumulaQuery
{
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IJogoRepository _jogoRepository;
    private readonly IFaseRepository _faseRepository;

    public RodizioDaSumulaQuery(ISumulaRepository sumulaRepository, IJogoRepository jogoRepository, IFaseRepository faseRepository)
    {
        _sumulaRepository = sumulaRepository;
        _jogoRepository = jogoRepository;
        _faseRepository = faseRepository;
    }

    public async Task<object?> ExecutarAsync(Guid sumulaId, CancellationToken ct)
    {
        var sumula = await _sumulaRepository.ObterPorIdAsync(sumulaId, ct);
        if (sumula is null) return null;

        var jogo = await _jogoRepository.ObterPorIdAsync(sumula.JogoId, ct);
        var fase = jogo is null ? null : await _faseRepository.ObterPorIdAsync(jogo.FaseId, ct);
        var categoria = fase?.TemporadaCategoria;
        var minimoQuadra = categoria?.MinimoPeriodosEmQuadra ?? 0;
        var minimoFora = categoria?.MinimoPeriodosForaQuadra ?? 0;
        var valor = categoria?.Temporada.ValorBonificacaoPorAtleta ?? 0m;

        object Lado(LadoTime lado)
        {
            var time = sumula.TimeDoLado(lado);
            var quadra = sumula.QuadraDo(time).ToHashSet();
            var rodizio = sumula.RodizioDo(time).ToDictionary(r => r.JogadorId);
            var exige = time.Jogadores.Count >= RegrasDeRelacao.MinimoParaRodizio;

            return new
            {
                TimeId = time.Id,
                time.Nome,
                time.CapitaoId,
                ExigeRodizio = exige,
                Relacionados = time.Jogadores.Count,
                Cumprem = rodizio.Values.Count(r => r.Cumpre(minimoQuadra, minimoFora)),
                BonificacaoPrevista = ServicoRodizio.Bonificacao(rodizio.Values.ToList(), minimoQuadra, minimoFora, valor),
                Jogadores = time.Jogadores
                    .OrderBy(j => int.TryParse(j.Numero, out var n) ? n : 0).ThenBy(j => j.Numero)
                    .Select(j =>
                    {
                        var r = rodizio[j.Id];
                        return new
                        {
                            j.Id,
                            j.Numero,
                            j.Nome,
                            j.Titular,
                            Capitao = time.CapitaoId == j.Id,
                            EmQuadra = quadra.Contains(j.Id),
                            Periodos = r.Periodos.Select(p => p.ToString()).ToList(),
                            r.CompletosEmQuadra,
                            r.CompletosFora,
                            Cumpre = r.Cumpre(minimoQuadra, minimoFora)
                        };
                    }).ToList()
            };
        }

        return new
        {
            PartidaId = sumula.Id,
            Status = sumula.Status.ToString(),
            Periodo = (int)sumula.PeriodoAtual,
            PeriodosEncerrados = sumula.PeriodosNormaisEncerrados,
            sumula.RelogioNoInicio,
            MinimoPeriodosEmQuadra = minimoQuadra,
            MinimoPeriodosForaQuadra = minimoFora,
            ValorBonificacaoPorAtleta = valor,
            Casa = Lado(LadoTime.Casa),
            Visitante = Lado(LadoTime.Visitante)
        };
    }
}
