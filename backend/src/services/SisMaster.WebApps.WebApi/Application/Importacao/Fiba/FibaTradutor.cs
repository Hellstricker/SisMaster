using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.WebApi.Application.Importacao.Fiba;

/// <summary>Lance do feed já traduzido para o vocabulário da súmula. O jogador ainda é só a camisa do time.</summary>
public sealed record EventoFiba(int Ordem, LadoTime Lado, string Camisa, TipoEvento Tipo, Periodo Periodo, int TempoRestanteSegundos);

/// <summary>Troca de quadra. <see cref="CamisaSai"/> nula = o jogador só entra (quadra com menos de 5).</summary>
public sealed record TrocaFiba(int Ordem, LadoTime Lado, string? CamisaSai, string CamisaEntra, Periodo Periodo, int TempoRestanteSegundos);

/// <summary>Lance atribuído à equipe, não a um jogador (rebote de equipe, turnover de equipe): não entra na súmula, mas aparece na conferência.</summary>
public sealed record EventoDeEquipeFiba(LadoTime Lado, TipoEvento Tipo, Periodo Periodo, int TempoRestanteSegundos);

public sealed class FibaTraducao
{
    public List<EventoFiba> Eventos { get; } = [];
    public List<TrocaFiba> Trocas { get; } = [];

    /// <summary>Lances do time sem jogador (rebote ou turnover de equipe, falta técnica do banco): a súmula não os guarda.</summary>
    public List<EventoDeEquipeFiba> EventosDeEquipe { get; } = [];
    public int EventosDeEquipeIgnorados => EventosDeEquipe.Count;

    /// <summary>Tipos de lance do feed que não têm equivalente (contagem por tipo/subtipo), fora os de controle do jogo.</summary>
    public Dictionary<string, int> NaoMapeados { get; } = [];

    /// <summary>Impeditivos da tradução (ex.: troca sem par, mais de uma prorrogação).</summary>
    public List<string> Problemas { get; } = [];
}

/// <summary>
/// Traduz o play-by-play do FIBA LiveStats em eventos e trocas da súmula. Função pura: não conhece a súmula nem o banco.
/// Convenção do feed (a mesma do SiteAbabas): time 1 = casa, time 2 = visitante (invertível).
/// </summary>
public static class FibaTradutor
{
    // Lances de controle do jogo que não viram evento nem pendência.
    private static readonly HashSet<string> Controle = ["game", "period", "jumpball", "foulon", "timeout"];

    public static FibaTraducao Traduzir(FibaJogoDto jogo, bool inverterLados = false)
    {
        var r = new FibaTraducao();
        var lances = jogo.Pbp.OrderBy(e => e.ActionNumber).ToList();

        var ordem = 0;
        foreach (var e in lances)
        {
            if (Controle.Contains(e.ActionType) || e.ActionType == "substitution") continue;
            if (!TentarLado(e.Tno, inverterLados, out var lado)) continue;

            if (!TentarPeriodo(e, r, out var periodo)) continue;
            var camisa = e.ShirtNumber?.Trim() ?? string.Empty;

            var tipo = TipoDe(e);
            if (tipo is null)
            {
                var chave = string.IsNullOrEmpty(e.SubType) ? e.ActionType : $"{e.ActionType}/{e.SubType}";
                r.NaoMapeados[chave] = r.NaoMapeados.GetValueOrDefault(chave) + 1;
                continue;
            }
            if (camisa.Length == 0) { r.EventosDeEquipe.Add(new EventoDeEquipeFiba(lado, tipo.Value, periodo, Segundos(e.Gt))); continue; }

            r.Eventos.Add(new EventoFiba(++ordem, lado, camisa, tipo.Value, periodo, Segundos(e.Gt)));
        }

        TraduzirTrocas(lances, inverterLados, r);
        return r;
    }

    private static TipoEvento? TipoDe(FibaEventoDto e)
    {
        var acertou = e.Success == 1;
        return e.ActionType switch
        {
            "2pt" => acertou ? TipoEvento.Ponto2 : TipoEvento.Erro2,
            "3pt" => acertou ? TipoEvento.Ponto3 : TipoEvento.Erro3,
            "freethrow" => acertou ? TipoEvento.LanceLivre : TipoEvento.ErroLance,
            "rebound" when e.SubType == "offensive" => TipoEvento.ReboteOfensivo,
            "rebound" when e.SubType == "defensive" => TipoEvento.ReboteDefensivo,
            "assist" => TipoEvento.Assistencia,
            "steal" => TipoEvento.Roubada,
            "block" => TipoEvento.Toco,
            "turnover" => TipoEvento.Turnover,
            // A falta ofensiva também é falta pessoal (conta nas 5 do jogador, igual ao box score do feed).
            "foul" when e.SubType is "personal" or "offensive" => TipoEvento.FaltaPessoal,
            "foul" when e.SubType == "technical" => TipoEvento.FaltaTecnica,
            "foul" when e.SubType == "unsportsmanlike" => TipoEvento.FaltaAntiDesportiva,
            _ => null
        };
    }

    /// <summary>
    /// No feed, a troca vem como lances "out" e "in" seguidos, no mesmo período e relógio. Dentro de cada grupo,
    /// o n-ésimo que sai é trocado pelo n-ésimo que entra; entrada sobrando = jogador que só entra.
    /// </summary>
    private static void TraduzirTrocas(List<FibaEventoDto> lances, bool inverter, FibaTraducao r)
    {
        var grupos = lances
            .Where(e => e.ActionType == "substitution" && e.Tno is 1 or 2)
            .GroupBy(e => (e.Tno, e.Period, e.PeriodType, e.Gt))
            .OrderBy(g => g.Min(e => e.ActionNumber));

        var ordem = 0;
        foreach (var g in grupos)
        {
            TentarLado(g.Key.Tno, inverter, out var lado);
            var temp = new FibaEventoDto { Period = g.Key.Period, PeriodType = g.Key.PeriodType };
            if (!TentarPeriodo(temp, r, out var periodo)) continue;

            var saem = g.Where(e => e.SubType == "out").OrderBy(e => e.ActionNumber).Select(e => e.ShirtNumber?.Trim() ?? "").ToList();
            var entram = g.Where(e => e.SubType == "in").OrderBy(e => e.ActionNumber).Select(e => e.ShirtNumber?.Trim() ?? "").ToList();
            var tempo = Segundos(g.Key.Gt);

            if (saem.Count > entram.Count)
                r.Problemas.Add($"Troca sem par no {periodo}º período ({g.Key.Gt}): {saem.Count} saíram e {entram.Count} entraram");

            for (var i = 0; i < entram.Count; i++)
                r.Trocas.Add(new TrocaFiba(++ordem, lado, i < saem.Count ? saem[i] : null, entram[i], periodo, tempo));
        }
    }

    private static bool TentarLado(int tno, bool inverter, out LadoTime lado)
    {
        lado = default;
        if (tno is not (1 or 2)) return false;
        lado = (tno == 1) != inverter ? LadoTime.Casa : LadoTime.Visitante;
        return true;
    }

    private static bool TentarPeriodo(FibaEventoDto e, FibaTraducao r, out Periodo periodo)
    {
        periodo = default;
        if (string.Equals(e.PeriodType, "OVERTIME", StringComparison.OrdinalIgnoreCase))
        {
            if (e.Period > 1 && !r.Problemas.Contains(MsgVariasProrrogacoes))
                r.Problemas.Add(MsgVariasProrrogacoes);
            periodo = Periodo.Prorrogacao;
            return e.Period <= 1;
        }
        if (e.Period is < 1 or > 4)
        {
            var msg = $"Período {e.Period} do feed não existe na súmula";
            if (!r.Problemas.Contains(msg)) r.Problemas.Add(msg);
            return false;
        }
        periodo = (Periodo)e.Period;
        return true;
    }

    private const string MsgVariasProrrogacoes = "O feed tem mais de uma prorrogação; a súmula só comporta uma";

    /// <summary>"mm:ss" → segundos.</summary>
    public static int Segundos(string? gt)
    {
        if (string.IsNullOrWhiteSpace(gt)) return 0;
        var partes = gt.Split(':');
        return partes.Length >= 2 && int.TryParse(partes[0], out var m) && int.TryParse(partes[1], out var s) ? m * 60 + s : 0;
    }
}
