namespace SisMaster.WebApps.WebApi.Domain.Jogos;

/// <summary>Quem disputa a classificação (uma vaga da fase) e o nome usado no desempate final.</summary>
public sealed record Participante(Guid Id, string Nome);

/// <summary>Um jogo que já tem resultado (Encerrado ou W.O.), com a bonificação ganha por cada lado.</summary>
public sealed record ResultadoJogo(Guid CasaId, Guid VisitanteId, int PlacarCasa, int PlacarVisitante, bool WO,
    decimal BonificacaoCasa = 0, decimal BonificacaoVisitante = 0)
{
    public Guid VencedorId => PlacarCasa >= PlacarVisitante ? CasaId : VisitanteId;
    public Guid PerdedorId => PlacarCasa >= PlacarVisitante ? VisitanteId : CasaId;
}

public sealed class LinhaClassificacao
{
    public const int PontosVitoria = 2;
    public const int PontosDerrota = 1;
    public const int PontosWO = 0;

    public Participante Participante { get; }
    public int Jogos { get; internal set; }
    public int Vitorias { get; internal set; }
    public int Derrotas { get; internal set; }
    public int DerrotasWO { get; internal set; }
    public int PontosFeitos { get; internal set; }
    public int PontosSofridos { get; internal set; }
    public decimal Bonificacao { get; internal set; }
    public List<char> Sequencia { get; } = [];

    /// <summary>Empate que nenhum critério resolveu: a ordem é alfabética e a diretoria decide por sorteio.</summary>
    public bool EmpatePorSorteio { get; internal set; }

    /// <summary>A diretoria já registrou o sorteio deste empate: a ordem segue o sorteio, não o nome.</summary>
    public bool SorteioDefinido { get; internal set; }

    public LinhaClassificacao(Participante participante) => Participante = participante;

    public int PontosPorVitorias => Vitorias * PontosVitoria;
    public int PontosPorDerrotas => Derrotas * PontosDerrota;
    public int PontosPorWO => DerrotasWO * PontosWO;

    /// <summary>Vitórias + derrotas + W.O., sem a bonificação.</summary>
    public int Subtotal => PontosPorVitorias + PontosPorDerrotas + PontosPorWO;

    /// <summary>Subtotal + somatório da bonificação.</summary>
    public decimal Total => Subtotal + Bonificacao;
    public int Saldo => PontosFeitos - PontosSofridos;
}

/// <summary>
/// Classificação de uma tabela (fase de pontos corridos ou um grupo). Vitória 2, derrota 1, W.O. 0, mais a
/// bonificação. Desempate em cascata, só entre quem continua empatado: (1) pontos, (2) saldo e (3) pontos feitos
/// no confronto direto — recalculados entre os empatados a cada nível —, (4) saldo geral, (5) pontos feitos
/// gerais e (6) sorteio. Função pura: nada é gravado.
/// </summary>
public static class ServicoClassificacao
{
    public static IReadOnlyList<LinhaClassificacao> Classificar(IReadOnlyCollection<Participante> participantes, IReadOnlyCollection<ResultadoJogo> jogos,
        IReadOnlyList<Guid>? ordemDoSorteio = null)
    {
        var validos = jogos.Where(j => participantes.Any(p => p.Id == j.CasaId) && participantes.Any(p => p.Id == j.VisitanteId)).ToList();
        var geral = Agregar(participantes, validos);
        var nomes = participantes.ToDictionary(p => p.Id, p => p.Nome);
        var sorteados = new HashSet<Guid>();

        var definidos = new HashSet<Guid>();
        var ordem = Desempatar(participantes.Select(p => p.Id).ToList(), validos, nomes, 1, sorteados, ordemDoSorteio, definidos);
        foreach (var id in sorteados) geral[id].EmpatePorSorteio = true;
        foreach (var id in definidos) geral[id].SorteioDefinido = true;
        return ordem.Select(id => geral[id]).ToList();
    }

    /// <summary>Melhores (N+1)º colocados de grupos diferentes: pontos, saldo, pontos feitos e, por fim, nome.</summary>
    public static IReadOnlyList<LinhaClassificacao> OrdenarMelhores(IEnumerable<LinhaClassificacao> candidatas) =>
        candidatas.OrderByDescending(l => l.Total).ThenByDescending(l => l.Saldo).ThenByDescending(l => l.PontosFeitos)
            .ThenBy(l => l.Participante.Nome, StringComparer.InvariantCultureIgnoreCase).ToList();

    private static Dictionary<Guid, LinhaClassificacao> Agregar(IEnumerable<Participante> participantes, IEnumerable<ResultadoJogo> jogos)
    {
        var linhas = participantes.ToDictionary(p => p.Id, p => new LinhaClassificacao(p));
        foreach (var j in jogos)
        {
            if (!linhas.TryGetValue(j.CasaId, out var casa) || !linhas.TryGetValue(j.VisitanteId, out var visitante)) continue;
            var vencedor = linhas[j.VencedorId];
            var perdedor = linhas[j.PerdedorId];

            casa.Jogos++; visitante.Jogos++;
            casa.PontosFeitos += j.PlacarCasa; casa.PontosSofridos += j.PlacarVisitante;
            visitante.PontosFeitos += j.PlacarVisitante; visitante.PontosSofridos += j.PlacarCasa;
            casa.Bonificacao += j.BonificacaoCasa; visitante.Bonificacao += j.BonificacaoVisitante;

            vencedor.Vitorias++; vencedor.Sequencia.Add('V');
            if (j.WO) perdedor.DerrotasWO++; else perdedor.Derrotas++;
            perdedor.Sequencia.Add('D');
        }
        return linhas;
    }

    private static List<Guid> Desempatar(List<Guid> ids, List<ResultadoJogo> jogosDaTabela, Dictionary<Guid, string> nomes, int passo, HashSet<Guid> sorteados,
        IReadOnlyList<Guid>? ordemDoSorteio, HashSet<Guid> definidos)
    {
        if (ids.Count <= 1) return ids;

        if (passo > 5)
        {
            foreach (var id in ids) sorteados.Add(id);
            if (ordemDoSorteio is not null && ids.All(ordemDoSorteio.Contains))
            {
                foreach (var id in ids) definidos.Add(id);
                return ids.OrderBy(id => ordemDoSorteio.ToList().IndexOf(id)).ToList();
            }
            return ids.OrderBy(id => nomes[id], StringComparer.InvariantCultureIgnoreCase).ToList();
        }

        // Passos 1–3: só os jogos entre os empatados; 4–5: todos os jogos da tabela.
        var jogosBase = passo <= 3
            ? jogosDaTabela.Where(j => ids.Contains(j.CasaId) && ids.Contains(j.VisitanteId)).ToList()
            : jogosDaTabela;
        var stats = Agregar(ids.Select(id => new Participante(id, nomes[id])), jogosBase);
        Func<LinhaClassificacao, decimal> chave = passo switch
        {
            1 => l => l.Total,
            2 or 4 => l => l.Saldo,
            _ => l => l.PontosFeitos
        };

        var resultado = new List<Guid>();
        foreach (var grupo in ids.GroupBy(id => chave(stats[id])).OrderByDescending(g => g.Key))
        {
            var lista = grupo.ToList();
            resultado.AddRange(lista.Count == 1 ? lista : Desempatar(lista, jogosDaTabela, nomes, passo + 1, sorteados, ordemDoSorteio, definidos));
        }
        return resultado;
    }
}

/// <summary>Placar de uma série de mata-mata (melhor de N): quem chega à maioria das vitórias vence.</summary>
public static class ServicoSerie
{
    public static int VitoriasNecessarias(int jogosPorConfronto) => jogosPorConfronto / 2 + 1;

    /// <summary>Vencedor da série, ou nulo se ainda não foi decidida.</summary>
    public static Guid? Vencedor(Guid ladoA, Guid ladoB, IEnumerable<ResultadoJogo> jogos, int jogosPorConfronto)
    {
        var necessarias = VitoriasNecessarias(jogosPorConfronto);
        var (a, b) = Placar(ladoA, ladoB, jogos);
        return a >= necessarias ? ladoA : b >= necessarias ? ladoB : null;
    }

    public static (int A, int B) Placar(Guid ladoA, Guid ladoB, IEnumerable<ResultadoJogo> jogos)
    {
        var lista = jogos.ToList();
        return (lista.Count(j => j.VencedorId == ladoA), lista.Count(j => j.VencedorId == ladoB));
    }
}
