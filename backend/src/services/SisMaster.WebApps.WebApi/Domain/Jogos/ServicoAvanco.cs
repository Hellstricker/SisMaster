using SisMaster.WebApps.WebApi.Domain.Fases;

namespace SisMaster.WebApps.WebApi.Domain.Jogos;

/// <summary>Resultado de um avanço: o que mudou na temporada por causa de um jogo que terminou.</summary>
public sealed class ResultadoAvanco
{
    public List<Jogo> Dispensados { get; } = [];
    public List<FaseEquipe> VagasResolvidas { get; } = [];

    /// <summary>Vagas que esperam um desempate por sorteio (a diretoria precisa decidir).</summary>
    public List<FaseEquipe> VagasAguardandoSorteio { get; } = [];
}

/// <summary>
/// Faz a temporada andar sozinha depois de cada jogo que termina: dispensa os jogos "se necessário" de séries já
/// decididas e resolve as vagas cuja origem terminou ("1º do Grupo A", "Vencedor — Quartas, Confronto 1"),
/// repetindo até nada mais mudar (uma série decidida pode destravar a fase seguinte). Muda as entidades recebidas;
/// quem chama persiste. Função sem acesso a banco.
/// </summary>
public static class ServicoAvanco
{
    public static ResultadoAvanco Avancar(IReadOnlyCollection<Fase> fases, IReadOnlyCollection<Grupo> grupos,
        IReadOnlyCollection<FaseEquipe> vagas, IReadOnlyCollection<Confronto> confrontos, IReadOnlyCollection<Jogo> jogos,
        IReadOnlyDictionary<Guid, (decimal Casa, decimal Visitante)>? bonificacoes = null,
        IReadOnlyCollection<SorteioDeDesempate>? sorteios = null)
    {
        bonificacoes ??= new Dictionary<Guid, (decimal, decimal)>();
        sorteios ??= [];
        var resultado = new ResultadoAvanco();
        var fasePorId = fases.ToDictionary(f => f.Id);
        var vagaPorId = vagas.ToDictionary(v => v.Id);

        bool mudou;
        do
        {
            mudou = DispensarSeriesDecididas(confrontos, jogos, fasePorId, resultado);
            mudou |= ResolverVagas(grupos, vagas, confrontos, jogos, fasePorId, vagaPorId, bonificacoes, sorteios, resultado);
        } while (mudou);

        return resultado;
    }

    private static bool Concluido(Jogo j) =>
        j.Status is StatusJogo.Encerrado or StatusJogo.WO && j.PlacarCasa is not null && j.PlacarVisitante is not null;

    private static ResultadoJogo Resultado(Jogo j, IReadOnlyDictionary<Guid, (decimal Casa, decimal Visitante)>? bonificacoes = null)
    {
        var (casa, visitante) = bonificacoes is not null && bonificacoes.TryGetValue(j.Id, out var b) ? b : (0m, 0m);
        return new(j.CasaId, j.VisitanteId, j.PlacarCasa!.Value, j.PlacarVisitante!.Value, j.Status == StatusJogo.WO, casa, visitante);
    }

    /// <summary>Vencedor e perdedor (vagas) da série, ou nulo se ainda não foi decidida.</summary>
    private static (Guid Vencedor, Guid Perdedor)? Serie(Confronto c, IReadOnlyCollection<Jogo> jogos, IReadOnlyDictionary<Guid, Fase> fases)
    {
        var doConfronto = jogos.Where(j => j.ConfrontoId == c.Id).OrderBy(j => j.JogoDaSerie).ThenBy(j => j.Numero).ToList();
        var primeiro = doConfronto.FirstOrDefault();
        if (primeiro is null) return null;

        var jogosPorConfronto = fases[c.FaseId].JogosPorConfronto ?? 1;
        var resultados = doConfronto.Where(Concluido).Select(j => Resultado(j)).ToList();
        var vencedor = ServicoSerie.Vencedor(primeiro.CasaId, primeiro.VisitanteId, resultados, jogosPorConfronto);
        if (vencedor is null) return null;
        return (vencedor.Value, vencedor == primeiro.CasaId ? primeiro.VisitanteId : primeiro.CasaId);
    }

    private static bool DispensarSeriesDecididas(IReadOnlyCollection<Confronto> confrontos, IReadOnlyCollection<Jogo> jogos,
        IReadOnlyDictionary<Guid, Fase> fases, ResultadoAvanco resultado)
    {
        var mudou = false;
        foreach (var c in confrontos)
        {
            if (Serie(c, jogos, fases) is null) continue;
            foreach (var j in jogos.Where(j => j.ConfrontoId == c.Id && j.Status == StatusJogo.Agendado))
            {
                j.Dispensar();
                resultado.Dispensados.Add(j);
                mudou = true;
            }
        }
        return mudou;
    }

    private static bool ResolverVagas(IReadOnlyCollection<Grupo> grupos, IReadOnlyCollection<FaseEquipe> vagas,
        IReadOnlyCollection<Confronto> confrontos, IReadOnlyCollection<Jogo> jogos,
        IReadOnlyDictionary<Guid, Fase> fases, IReadOnlyDictionary<Guid, FaseEquipe> vagaPorId,
        IReadOnlyDictionary<Guid, (decimal Casa, decimal Visitante)> bonificacoes, IReadOnlyCollection<SorteioDeDesempate> sorteios, ResultadoAvanco resultado)
    {
        var mudou = false;
        foreach (var vaga in vagas.Where(v => v.EquipeId is null))
        {
            var origem = vaga.Origem;
            Guid? equipeId = null;

            switch (origem.Tipo)
            {
                case TipoReferencia.Colocacao when origem.FaseId is not null && origem.Posicao is not null:
                    equipeId = EquipeDaColocacao(vaga, origem, grupos, vagas, jogos, fases, bonificacoes, sorteios, resultado);
                    break;

                case TipoReferencia.Vencedor or TipoReferencia.Perdedor when origem.ConfrontoOrigemId is not null:
                    var confronto = confrontos.FirstOrDefault(c => c.Id == origem.ConfrontoOrigemId);
                    if (confronto is not null && Serie(confronto, jogos, fases) is { } serie)
                        equipeId = vagaPorId[origem.Tipo == TipoReferencia.Vencedor ? serie.Vencedor : serie.Perdedor].EquipeId;
                    break;
            }

            if (equipeId is null) continue;
            vaga.ResolverEquipe(equipeId.Value);
            resultado.VagasResolvidas.Add(vaga);
            mudou = true;
        }
        return mudou;
    }

    private static Guid? EquipeDaColocacao(FaseEquipe vaga, ReferenciaEquipe origem, IReadOnlyCollection<Grupo> grupos,
        IReadOnlyCollection<FaseEquipe> vagas, IReadOnlyCollection<Jogo> jogos, IReadOnlyDictionary<Guid, Fase> fases,
        IReadOnlyDictionary<Guid, (decimal Casa, decimal Visitante)> bonificacoes, IReadOnlyCollection<SorteioDeDesempate> sorteios, ResultadoAvanco resultado)
    {
        if (!fases.TryGetValue(origem.FaseId!.Value, out var faseOrigem) || faseOrigem.Tipo == TipoFase.MataMata) return null;

        // "Nº classificado" de uma fase de grupos (sem indicar o grupo): ranking geral dos classificados.
        if (faseOrigem.Tipo == TipoFase.Grupos && origem.GrupoOrdem is null)
            return EquipeDaClassificacaoGeral(vaga, origem, faseOrigem, grupos, vagas, jogos, bonificacoes, sorteios, resultado);

        Guid? grupoId = null;
        if (origem.GrupoOrdem is not null)
        {
            grupoId = grupos.FirstOrDefault(g => g.FaseId == faseOrigem.Id && g.Ordem == origem.GrupoOrdem)?.Id;
            if (grupoId is null) return null;
        }

        var tabela = ClassificarTabela(faseOrigem.Id, grupoId, vagas, jogos, bonificacoes, sorteios);
        if (tabela is null || origem.Posicao > tabela.Value.Linhas.Count) return null;

        var linha = tabela.Value.Linhas[origem.Posicao!.Value - 1];
        if (linha.EmpatePorSorteio && !linha.SorteioDefinido)
        {
            resultado.VagasAguardandoSorteio.Add(vaga);
            return null;
        }
        return tabela.Value.Vagas.First(v => v.Id == linha.Participante.Id).EquipeId;
    }

    /// <summary>
    /// Ranking geral dos classificados de uma fase de grupos: os N primeiros de cada grupo (mais os M melhores (N+1)º),
    /// ordenados por posição no grupo e, dentro da mesma posição, por pontos, saldo e pontos feitos.
    /// Só existe quando todos os grupos terminaram e nenhum empate sem critério ficou sem sorteio.
    /// </summary>
    private static Guid? EquipeDaClassificacaoGeral(FaseEquipe vaga, ReferenciaEquipe origem, Fase faseOrigem, IReadOnlyCollection<Grupo> grupos,
        IReadOnlyCollection<FaseEquipe> vagas, IReadOnlyCollection<Jogo> jogos,
        IReadOnlyDictionary<Guid, (decimal Casa, decimal Visitante)> bonificacoes, IReadOnlyCollection<SorteioDeDesempate> sorteios, ResultadoAvanco resultado)
    {
        var tabelas = new List<(List<LinhaClassificacao> Linhas, List<FaseEquipe> Vagas)>();
        foreach (var grupo in grupos.Where(g => g.FaseId == faseOrigem.Id).OrderBy(g => g.Ordem))
        {
            var tabela = ClassificarTabela(faseOrigem.Id, grupo.Id, vagas, jogos, bonificacoes, sorteios);
            if (tabela is null) return null;
            if (tabela.Value.Linhas.Any(l => l.EmpatePorSorteio && !l.SorteioDefinido))
            {
                resultado.VagasAguardandoSorteio.Add(vaga);
                return null;
            }
            tabelas.Add((tabela.Value.Linhas.ToList(), tabela.Value.Vagas));
        }
        if (tabelas.Count == 0) return null;

        var n = faseOrigem.ClassificadosPrimeiros ?? int.MaxValue;
        var classificados = tabelas.SelectMany(t => t.Linhas.Take(n).Select((l, i) => (Linha: l, Posicao: i + 1, t.Vagas))).ToList();
        if (faseOrigem.ClassificadosPrimeiros is { } primeiros && faseOrigem.MelhoresExtras > 0)
        {
            var candidatas = tabelas.Where(t => t.Linhas.Count > primeiros).Select(t => (Linha: t.Linhas[primeiros], t.Vagas)).ToList();
            var melhores = ServicoClassificacao.OrdenarMelhores(candidatas.Select(c => c.Linha)).Take(faseOrigem.MelhoresExtras).ToList();
            classificados.AddRange(candidatas.Where(c => melhores.Contains(c.Linha)).Select(c => (c.Linha, primeiros + 1, c.Vagas)));
        }

        var ranking = classificados
            .OrderBy(c => c.Posicao).ThenByDescending(c => c.Linha.Total).ThenByDescending(c => c.Linha.Saldo)
            .ThenByDescending(c => c.Linha.PontosFeitos).ThenBy(c => c.Linha.Participante.Nome, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
        if (origem.Posicao > ranking.Count) return null;

        var escolhido = ranking[origem.Posicao!.Value - 1];
        return escolhido.Vagas.First(v => v.Id == escolhido.Linha.Participante.Id).EquipeId;
    }

    /// <summary>Classificação de uma tabela já terminada (todas as vagas com equipe e todos os jogos concluídos); senão, nulo.</summary>
    private static (IReadOnlyList<LinhaClassificacao> Linhas, List<FaseEquipe> Vagas)? ClassificarTabela(Guid faseId, Guid? grupoId,
        IReadOnlyCollection<FaseEquipe> vagas, IReadOnlyCollection<Jogo> jogos,
        IReadOnlyDictionary<Guid, (decimal Casa, decimal Visitante)> bonificacoes, IReadOnlyCollection<SorteioDeDesempate> sorteios)
    {
        var daTabela = vagas.Where(v => v.FaseId == faseId && v.GrupoId == grupoId).ToList();
        var jogosDaTabela = jogos.Where(j => j.FaseId == faseId && j.GrupoId == grupoId).ToList();

        if (jogosDaTabela.Count == 0 || daTabela.Any(v => v.EquipeId is null)) return null;
        if (!jogosDaTabela.All(j => Concluido(j) || j.Status == StatusJogo.Dispensado)) return null;

        var participantes = daTabela.Select(v => new Participante(v.Id, v.EquipeId.ToString()!)).ToList();
        var ordemDoSorteio = sorteios.FirstOrDefault(x => x.FaseId == faseId && x.GrupoId == grupoId)?.OrdemDasVagas;
        var linhas = ServicoClassificacao.Classificar(participantes, jogosDaTabela.Where(Concluido).Select(j => Resultado(j, bonificacoes)).ToList(), ordemDoSorteio);
        return (linhas, daTabela);
    }
}
