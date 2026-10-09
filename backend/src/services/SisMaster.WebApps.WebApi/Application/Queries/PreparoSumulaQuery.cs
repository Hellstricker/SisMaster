using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;
using SisMaster.WebApps.WebApi.Domain.Jogos;
using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.WebApi.Application.Queries;

/// <summary>Tela 11A: a súmula do jogo para preparar a relação (elenco de cada equipe, o que já foi relacionado e o que falta).</summary>
public class PreparoSumulaQuery
{
    private readonly IJogoRepository _jogoRepository;
    private readonly IFaseRepository _faseRepository;
    private readonly ISumulaRepository _sumulaRepository;
    private readonly IEquipeRepository _equipeRepository;

    public PreparoSumulaQuery(IJogoRepository jogoRepository, IFaseRepository faseRepository,
        ISumulaRepository sumulaRepository, IEquipeRepository equipeRepository)
    {
        _jogoRepository = jogoRepository;
        _faseRepository = faseRepository;
        _sumulaRepository = sumulaRepository;
        _equipeRepository = equipeRepository;
    }

    public async Task<object?> ExecutarAsync(Guid jogoId, CancellationToken ct)
    {
        var jogo = await _jogoRepository.ObterPorIdAsync(jogoId, ct);
        if (jogo is null) return null;

        var fase = (await _faseRepository.ObterPorIdAsync(jogo.FaseId, ct))!;
        var categoria = fase.TemporadaCategoria;
        var sumula = await _sumulaRepository.ObterPorJogoAsync(jogoId, ct);

        var times = new List<object>();
        if (sumula is not null)
        {
            foreach (var t in sumula.Times.OrderBy(t => t.Lado))
            {
                var elenco = await _equipeRepository.ObterElencoAsync(t.EquipeId, ct);
                var capitao = t.Jogadores.FirstOrDefault(j => j.Id == t.CapitaoId);
                times.Add(new
                {
                    t.Lado,
                    t.EquipeId,
                    t.Nome,
                    t.Cor,
                    t.Tecnico,
                    t.AuxiliarTecnico,
                    CapitaoAtletaId = capitao?.AtletaId,
                    Elenco = elenco.Select(a => new { a.AtletaId, a.Nome }),
                    Jogadores = t.Jogadores
                        .OrderBy(j => int.TryParse(j.Numero, out var n) ? n : 0).ThenBy(j => j.Numero)
                        .Select(j => new { j.Id, j.AtletaId, j.Nome, j.Numero, j.Titular, j.ChegouNoPeriodo }),
                    Pendencias = t.Pendencias()
                });
            }
        }

        return new
        {
            Jogo = new { jogo.Id, jogo.Numero, jogo.Status, jogo.Data, jogo.Hora, Categoria = categoria.Nome, Fase = fase.Nome },
            Existe = sumula is not null,
            Sumula = sumula is null ? null : new
            {
                sumula.Id,
                sumula.Status,
                // A relação só muda enquanto a súmula está EmPreparacao.
                Editavel = sumula.Status == StatusSumula.EmPreparacao,
                // Depois do início do jogo ainda é possível acrescentar atletas (chegada tardia).
                PodeAcrescentarAtletas = sumula.Status is StatusSumula.EmAndamento or StatusSumula.Intervalo,
                sumula.PlacarCasa,
                sumula.PlacarVisitante,
                sumula.CodigoExterno,
                sumula.ImportadaEm
            },
            Times = times,
            // Sem rodízio na categoria (mínimos em quadra e fora zerados), vale só o mínimo da FIBA.
            CategoriaComRodizio = categoria.MinimoPeriodosEmQuadra > 0 || categoria.MinimoPeriodosForaQuadra > 0,
            Regras = new
            {
                Maximo = RegrasDeRelacao.Maximo,
                MinimoParaIniciar = RegrasDeRelacao.MinimoParaIniciar,
                Titulares = RegrasDeRelacao.Titulares,
                MinimoParaRodizio = RegrasDeRelacao.MinimoParaRodizio
            }
        };
    }
}
