namespace SisMaster.WebApps.WebApi.Domain.Sumula;

public enum EstadoPeriodo
{
    /// <summary>Em quadra do início ao fim do período.</summary>
    Completo,

    /// <summary>No banco do início ao fim do período.</summary>
    Fora,

    /// <summary>Entrou ou saiu no meio do período: não conta para nenhum dos lados.</summary>
    Parcial,

    /// <summary>Ainda não estava na súmula (chegou depois do início do jogo): o período não conta para ele.</summary>
    Ausente
}

/// <summary>Uma substituição reduzida ao que o rodízio precisa saber.</summary>
public sealed record TrocaDeQuadra(Guid? SaiId, Guid EntraId, int Periodo, bool NoInicio);

/// <summary>Um relacionado para o cálculo do rodízio, com a chegada tardia (se houve).</summary>
public sealed record ParticipanteRodizio(Guid Id, bool Titular, int? ChegouNoPeriodo = null, bool ChegouNoInicio = true);

/// <summary>Situação de um jogador em cada um dos períodos normais já encerrados (posição 0 = 1º período).</summary>
public sealed record RodizioDoJogador(Guid JogadorId, IReadOnlyList<EstadoPeriodo> Periodos)
{
    public int CompletosEmQuadra => Periodos.Count(p => p == EstadoPeriodo.Completo);
    public int CompletosFora => Periodos.Count(p => p == EstadoPeriodo.Fora);

    public bool Cumpre(int minimoEmQuadra, int minimoFora) =>
        CompletosEmQuadra >= minimoEmQuadra && CompletosFora >= minimoFora;
}

/// <summary>
/// Rodízio obrigatório derivado das substituições (nunca digitado): a quadra começa com os titulares; em cada período
/// o jogador está "completo" (em quadra do início ao fim), "fora" (no banco do início ao fim) ou "parcial".
/// Trocas feitas antes de o relógio do período correr valem como início do período. Só os períodos normais já
/// encerrados entram na conta. Função pura.
/// </summary>
public static class ServicoRodizio
{
    public static IReadOnlyList<RodizioDoJogador> Calcular(IReadOnlyCollection<(Guid Id, bool Titular)> jogadores,
        IReadOnlyList<TrocaDeQuadra> trocas, int periodosEncerrados) =>
        Calcular(jogadores.Select(j => new ParticipanteRodizio(j.Id, j.Titular)).ToList(), trocas, periodosEncerrados);

    public static IReadOnlyList<RodizioDoJogador> Calcular(IReadOnlyCollection<ParticipanteRodizio> jogadores,
        IReadOnlyList<TrocaDeQuadra> trocas, int periodosEncerrados)
    {
        var periodos = Math.Clamp(periodosEncerrados, 0, 4);
        var resultado = new List<RodizioDoJogador>();

        foreach (var jogador in jogadores)
        {
            var id = jogador.Id;
            var estados = new List<EstadoPeriodo>();
            for (var p = 1; p <= periodos; p++)
            {
                if (jogador.ChegouNoPeriodo is { } chegou && (p < chegou || (p == chegou && !jogador.ChegouNoInicio)))
                {
                    estados.Add(p < chegou ? EstadoPeriodo.Ausente : EstadoPeriodo.Parcial);
                    continue;
                }

                var noInicio = QuadraApos(jogadores, trocas, t => t.Periodo < p || (t.Periodo == p && t.NoInicio)).Contains(id);
                var noFim = QuadraApos(jogadores, trocas, t => t.Periodo <= p).Contains(id);
                var mexeuNoMeio = trocas.Any(t => t.Periodo == p && !t.NoInicio && (t.SaiId == id || t.EntraId == id));

                estados.Add(mexeuNoMeio ? EstadoPeriodo.Parcial
                    : noInicio && noFim ? EstadoPeriodo.Completo
                    : !noInicio && !noFim ? EstadoPeriodo.Fora
                    : EstadoPeriodo.Parcial);
            }
            resultado.Add(new RodizioDoJogador(id, estados));
        }
        return resultado;
    }

    private static HashSet<Guid> QuadraApos(IReadOnlyCollection<ParticipanteRodizio> jogadores,
        IReadOnlyList<TrocaDeQuadra> trocas, Func<TrocaDeQuadra, bool> aplicar)
    {
        var quadra = jogadores.Where(j => j.Titular).Select(j => j.Id).ToHashSet();
        foreach (var t in trocas.Where(aplicar))
            if (t.SaiId is null || quadra.Remove(t.SaiId.Value)) quadra.Add(t.EntraId);
        return quadra;
    }

    /// <summary>
    /// Bonificação de um time no jogo: atletas que cumpriram o rodízio × valor por atleta. Time com menos de
    /// <see cref="RegrasDeRelacao.MinimoParaRodizio"/> relacionados não exige rodízio e não ganha bonificação.
    /// </summary>
    public static decimal Bonificacao(IReadOnlyList<RodizioDoJogador> rodizio, int minimoEmQuadra, int minimoFora, decimal valorPorAtleta)
    {
        if (valorPorAtleta <= 0 || rodizio.Count < RegrasDeRelacao.MinimoParaRodizio) return 0;
        return rodizio.Count(j => j.Cumpre(minimoEmQuadra, minimoFora)) * valorPorAtleta;
    }
}
