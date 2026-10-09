using SisMaster.WebApps.WebApi.Domain.Sumula;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.WebApi.Application.Importacao.Fiba;

/// <summary>Um relacionado da súmula, como a prévia o enxerga.</summary>
public sealed record RelacionadoDaSumula(LadoTime Lado, string Numero, string Nome, bool Titular);

public sealed record PreviaJogadorFiba(
    string Camisa, string NomeFeed, string? NomeSumula, bool TitularFeed, bool? TitularSumula, bool Jogou, IReadOnlyList<string> Divergencias);

public sealed record PreviaPeriodoFiba(int Periodo, int? PlacarFeed, int PlacarReconstruido);

public sealed record PreviaTimeFiba(
    LadoTime Lado, string NomeFeed, string NomeSumula, int PlacarFeed, int PlacarReconstruido,
    IReadOnlyList<PreviaPeriodoFiba> Periodos, IReadOnlyList<PreviaJogadorFiba> Jogadores);

public sealed record PreviaImportacaoFiba(
    IReadOnlyList<PreviaTimeFiba> Times, int Eventos, int Trocas, IReadOnlyList<EventoDeEquipeFiba> EventosDeEquipe,
    IReadOnlyDictionary<string, int> NaoMapeados, IReadOnlyList<string> Problemas, IReadOnlyList<string> Avisos)
{
    /// <summary>Só dá para aplicar quando nada impede e tudo que o feed diz bate com o que foi reconstruído.</summary>
    public bool PodeAplicar => Problemas.Count == 0;
}

/// <summary>
/// Confronta o feed traduzido com a relação da súmula e confere o que foi reconstruído (placar, placar por período e
/// totais por jogador) contra os números oficiais do próprio feed. Função pura: nada é gravado.
/// </summary>
public static class FibaAnalisador
{
    public static PreviaImportacaoFiba Analisar(
        FibaJogoDto jogo, FibaTraducao traducao, IReadOnlyList<RelacionadoDaSumula> relacao,
        IReadOnlyDictionary<LadoTime, string> nomesDosTimes, bool inverterLados = false)
    {
        var problemas = new List<string>(traducao.Problemas);
        var avisos = new List<string>();
        var times = new List<PreviaTimeFiba>();

        foreach (var lado in new[] { LadoTime.Casa, LadoTime.Visitante })
        {
            var chave = ((lado == LadoTime.Casa) != inverterLados ? 1 : 2).ToString();
            if (!jogo.Tm.TryGetValue(chave, out var tm))
            {
                problemas.Add($"O feed não tem o time {chave}");
                continue;
            }
            times.Add(AnalisarTime(lado, tm, traducao, relacao.Where(r => r.Lado == lado).ToList(),
                nomesDosTimes.GetValueOrDefault(lado, string.Empty), problemas, avisos));
        }

        if (traducao.EventosDeEquipeIgnorados > 0)
            avisos.Add($"{traducao.EventosDeEquipeIgnorados} lance(s) de equipe (sem jogador) não entram na súmula — veja a lista abaixo");
        foreach (var (tipo, qtd) in traducao.NaoMapeados)
            avisos.Add($"{qtd} lance(s) \"{tipo}\" sem equivalente na súmula foram ignorados");

        return new PreviaImportacaoFiba(times, traducao.Eventos.Count, traducao.Trocas.Count,
            traducao.EventosDeEquipe, traducao.NaoMapeados, problemas, avisos);
    }

    private static PreviaTimeFiba AnalisarTime(
        LadoTime lado, FibaTimeDto tm, FibaTraducao tr, List<RelacionadoDaSumula> relacionados,
        string nomeSumula, List<string> problemas, List<string> avisos)
    {
        var eventos = tr.Eventos.Where(e => e.Lado == lado).ToList();
        var trocas = tr.Trocas.Where(t => t.Lado == lado).ToList();
        var rotulo = string.IsNullOrEmpty(nomeSumula) ? tm.Name : nomeSumula;

        if (!string.IsNullOrEmpty(nomeSumula) && Normalizar(nomeSumula) != Normalizar(tm.Name))
            avisos.Add($"{rotulo}: no feed o time se chama \"{tm.Name}\" — confirme se o lado (casa/visitante) está certo");

        // Placar reconstruído, total e por período.
        var pontos = eventos.Where(e => Pontos(e.Tipo) > 0).ToList();
        var total = pontos.Sum(e => Pontos(e.Tipo));
        if (total != tm.Score) problemas.Add($"{rotulo}: o placar reconstruído ({total}) difere do oficial ({tm.Score})");

        var periodos = new List<PreviaPeriodoFiba>();
        foreach (var p in new[] { 1, 2, 3, 4 })
        {
            var rec = pontos.Where(e => (int)e.Periodo == p).Sum(e => Pontos(e.Tipo));
            var oficial = tm.PlacarDoPeriodo(p);
            periodos.Add(new PreviaPeriodoFiba(p, oficial, rec));
            if (oficial is not null && oficial != rec)
                problemas.Add($"{rotulo}: no {p}º período o placar reconstruído ({rec}) difere do oficial ({oficial})");
        }

        // Jogadores: camisa do feed ↔ relação da súmula.
        var porCamisa = relacionados.ToDictionary(r => r.Numero, r => r);
        var usadas = eventos.Select(e => e.Camisa)
            .Concat(trocas.SelectMany(t => new[] { t.CamisaSai, t.CamisaEntra }).OfType<string>())
            .ToHashSet();

        var jogadores = new List<PreviaJogadorFiba>();
        foreach (var pl in tm.Pl.Values.OrderBy(p => int.TryParse(p.ShirtNumber, out var n) ? n : 0).ThenBy(p => p.ShirtNumber))
        {
            var camisa = pl.ShirtNumber.Trim();
            var jogou = usadas.Contains(camisa) || pl.Starter == 1;
            porCamisa.TryGetValue(camisa, out var rel);
            var divergencias = new List<string>();

            if (rel is null)
            {
                if (jogou) problemas.Add($"{rotulo}: a camisa {camisa} ({pl.NomeCompleto}) jogou mas não está relacionada na súmula");
                else avisos.Add($"{rotulo}: a camisa {camisa} ({pl.NomeCompleto}) está no feed, não jogou e não está relacionada");
            }
            else
            {
                if ((pl.Starter == 1) != rel.Titular)
                    avisos.Add($"{rotulo}: a camisa {camisa} ({rel.Nome}) {(pl.Starter == 1 ? "foi titular no feed e não na súmula" : "é titular na súmula e não foi no feed")} — ao importar, os titulares do feed prevalecem");
                ConferirTotais(pl, eventos.Where(e => e.Camisa == camisa).ToList(), divergencias);
                foreach (var d in divergencias) problemas.Add($"{rotulo}, camisa {camisa} ({rel.Nome}): {d}");
            }

            jogadores.Add(new PreviaJogadorFiba(camisa, pl.NomeCompleto, rel?.Nome, pl.Starter == 1, rel?.Titular, jogou, divergencias));
        }

        foreach (var r in relacionados.Where(r => tm.Pl.Values.All(p => p.ShirtNumber.Trim() != r.Numero)))
            avisos.Add($"{rotulo}: {r.Nome} (camisa {r.Numero}) está na súmula e não aparece no feed");

        var titularesDoFeed = tm.Pl.Values.Where(p => p.Starter == 1).Select(p => p.ShirtNumber.Trim()).ToHashSet();
        var esperados = Math.Min(5, relacionados.Count);
        if (titularesDoFeed.Count != esperados)
            problemas.Add($"{rotulo}: o feed indica {titularesDoFeed.Count} titulares (esperado {esperados})");
        ValidarQuadra(rotulo, titularesDoFeed, trocas, problemas);

        return new PreviaTimeFiba(lado, tm.Name, nomeSumula, tm.Score, total, periodos, jogadores);
    }

    /// <summary>Reproduz a quadra a partir dos titulares do feed (que prevalecem sobre os da súmula): toda troca precisa tirar quem está e pôr quem não está.</summary>
    private static void ValidarQuadra(string rotulo, IReadOnlyCollection<string> titularesDoFeed, List<TrocaFiba> trocas, List<string> problemas)
    {
        var quadra = titularesDoFeed.ToHashSet();
        foreach (var t in trocas)
        {
            if (t.CamisaSai is not null && !quadra.Remove(t.CamisaSai))
            {
                problemas.Add($"{rotulo}: troca da camisa {t.CamisaSai} no {(int)t.Periodo}º período, mas ela não estava em quadra");
                continue;
            }
            if (!quadra.Add(t.CamisaEntra))
                problemas.Add($"{rotulo}: a camisa {t.CamisaEntra} entrou no {(int)t.Periodo}º período, mas já estava em quadra");
        }
    }

    private static void ConferirTotais(FibaJogadorDto pl, List<EventoFiba> ev, List<string> divergencias)
    {
        void Comparar(string nome, int feed, int reconstruido)
        {
            if (feed != reconstruido) divergencias.Add($"{nome}: oficial {feed}, reconstruído {reconstruido}");
        }
        int N(Func<EventoFiba, bool> f) => ev.Count(f);

        Comparar("pontos", pl.SPoints, ev.Sum(e => Pontos(e.Tipo)));
        Comparar("faltas", pl.SFoulsPersonal, N(e => e.Tipo is TipoEvento.FaltaPessoal or TipoEvento.FaltaTecnica or TipoEvento.FaltaAntiDesportiva));
        Comparar("rebotes ofensivos", pl.SReboundsOffensive, N(e => e.Tipo == TipoEvento.ReboteOfensivo));
        Comparar("rebotes defensivos", pl.SReboundsDefensive, N(e => e.Tipo == TipoEvento.ReboteDefensivo));
        Comparar("assistências", pl.SAssists, N(e => e.Tipo == TipoEvento.Assistencia));
        Comparar("roubadas", pl.SSteals, N(e => e.Tipo == TipoEvento.Roubada));
        Comparar("tocos", pl.SBlocks, N(e => e.Tipo == TipoEvento.Toco));
        Comparar("turnovers", pl.STurnovers, N(e => e.Tipo == TipoEvento.Turnover));
    }

    private static int Pontos(TipoEvento t) => t switch
    {
        TipoEvento.Ponto2 => 2,
        TipoEvento.Ponto3 => 3,
        TipoEvento.LanceLivre => 1,
        _ => 0
    };

    private static string Normalizar(string s) => s.Trim().ToUpperInvariant();
}
