using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;

namespace SisMaster.WebApps.WebApi.Application.Services;

/// <summary>Bonificação de cada lado de um jogo (pontos que somam à classificação).</summary>
public readonly record struct BonificacaoDoJogo(decimal Casa, decimal Visitante);

/// <summary>
/// Calcula a bonificação por rodízio dos jogos encerrados, a partir das súmulas (substituições): cada atleta que cumpriu
/// o rodízio soma o valor da bonificação por atleta da temporada. Calculada na hora, nunca gravada. W.O. não bonifica
/// (não tem súmula); time com menos de 7 relacionados também não. Os mínimos vêm do snapshot da categoria na temporada.
/// </summary>
public class BonificacaoDosJogos
{
    private readonly ISumulaRepository _sumulaRepository;

    public BonificacaoDosJogos(ISumulaRepository sumulaRepository) => _sumulaRepository = sumulaRepository;

    /// <summary>As fases devem vir com <c>TemporadaCategoria.Temporada</c> carregados.</summary>
    public async Task<IReadOnlyDictionary<Guid, BonificacaoDoJogo>> CalcularAsync(IEnumerable<Jogo> jogos, IEnumerable<Fase> fases, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, BonificacaoDoJogo>();
        var encerrados = jogos.Where(j => j.Status == StatusJogo.Encerrado).ToList();
        if (encerrados.Count == 0) return resultado;

        var fasePorId = fases.ToDictionary(f => f.Id);
        var sumulas = (await _sumulaRepository.ListarPorJogosAsync(encerrados.Select(j => j.Id).ToList(), ct))
            .ToDictionary(s => s.JogoId);

        foreach (var jogo in encerrados)
        {
            if (!sumulas.TryGetValue(jogo.Id, out var sumula) || !fasePorId.TryGetValue(jogo.FaseId, out var fase)) continue;

            var categoria = fase.TemporadaCategoria;
            var valor = categoria.Temporada.ValorBonificacaoPorAtleta;
            if (valor <= 0) continue;

            decimal DoTime(LadoTime lado)
            {
                var time = sumula.TimeDoLado(lado);
                return ServicoRodizio.Bonificacao(sumula.RodizioDo(time), categoria.MinimoPeriodosEmQuadra, categoria.MinimoPeriodosForaQuadra, valor);
            }

            resultado[jogo.Id] = new BonificacaoDoJogo(DoTime(LadoTime.Casa), DoTime(LadoTime.Visitante));
        }
        return resultado;
    }
}
