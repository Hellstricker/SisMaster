using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Equipes;
using SisMaster.WebApps.WebApi.Domain.Fases;

namespace SisMaster.WebApps.WebApi.Domain.Jogos;

/// <summary>Tudo que a geração precisa saber de uma categoria da temporada.</summary>
public sealed record CategoriaParaGerar(
    Guid TemporadaCategoriaId,
    string Nome,
    IReadOnlyList<Equipe> Equipes,
    IReadOnlyList<Fase> Fases,
    IReadOnlyList<Confronto> Confrontos);

public sealed class TabelaGerada
{
    public List<Grupo> Grupos { get; } = [];
    public List<FaseEquipe> Vagas { get; } = [];
    public List<Jogo> Jogos { get; } = [];
}

/// <summary>
/// Monta a tabela de jogos da temporada a partir das fases: vagas de equipe por fase (com a origem de cada uma),
/// grupos, e os jogos (todos contra todos em pontos corridos e dentro de cada grupo; séries no mata-mata).
/// Função pura: não toca no banco, então serve também de prévia enquanto a tabela ainda não foi gerada.
/// Datas, horários e locais não fazem parte da geração (são definidos jogo a jogo).
/// </summary>
public static class ServicoGeracaoJogos
{
    public static TabelaGerada Gerar(Guid temporadaId, IEnumerable<CategoriaParaGerar> categorias)
    {
        var tabela = new TabelaGerada();
        var ordenadas = new List<(int Chave, Jogo Jogo)>();
        var chave = 0;

        foreach (var categoria in categorias.OrderBy(c => c.Nome, StringComparer.CurrentCultureIgnoreCase))
        {
            if (categoria.Fases.Count == 0) continue;
            var classificadosPorFase = new Dictionary<Guid, int>();

            foreach (var fase in categoria.Fases.OrderBy(f => f.Ordem))
            {
                try
                {
                    if (!fase.EstruturaCompleta)
                        throw new DomainException("a estrutura da fase está incompleta; edite a fase antes de gerar a tabela");

                    var jogosDaFase = fase.Tipo == TipoFase.MataMata
                        ? GerarMataMata(temporadaId, fase, categoria, tabela)
                        : GerarTodosContraTodos(temporadaId, fase, categoria, classificadosPorFase, tabela);

                    classificadosPorFase[fase.Id] = Classificados(fase, VagasDaFase(tabela, fase));

                    // Ordem de numeração: categoria, fase, rodada, grupo/confronto.
                    foreach (var j in jogosDaFase.OrderBy(j => j.Rodada).ThenBy(j => OrdemDoGrupo(tabela, j)).ThenBy(j => j.Numero))
                        ordenadas.Add((chave++, j));
                }
                catch (DomainException ex)
                {
                    throw new DomainException($"{categoria.Nome} › {fase.Nome}: {ex.Message}");
                }
            }
        }

        var numero = 1;
        foreach (var (_, jogo) in ordenadas.OrderBy(o => o.Chave))
            jogo.Numerar(numero++);

        return tabela;
    }

    // Os jogos nascem com Numero = 0 até a numeração final; este índice desempata jogos da mesma rodada por grupo/confronto.
    private static int OrdemDoGrupo(TabelaGerada tabela, Jogo jogo) =>
        jogo.GrupoId is null ? 0 : tabela.Grupos.First(g => g.Id == jogo.GrupoId).Ordem;

    private static int VagasDaFase(TabelaGerada tabela, Fase fase) =>
        tabela.Vagas.Count(v => v.FaseId == fase.Id);

    private static int Classificados(Fase fase, int vagas) => fase.Tipo switch
    {
        TipoFase.MataMata => fase.NumeroConfrontos!.Value,
        TipoFase.PontosCorridos => fase.ClassificadosPrimeiros ?? vagas,
        _ => fase.ClassificadosPrimeiros is null
            ? vagas
            : fase.ClassificadosPrimeiros.Value * fase.NumeroGrupos!.Value + fase.MelhoresExtras
    };

    private static List<Jogo> GerarMataMata(Guid temporadaId, Fase fase, CategoriaParaGerar categoria, TabelaGerada tabela)
    {
        var confrontos = categoria.Confrontos.Where(c => c.FaseId == fase.Id).OrderBy(c => c.Numero).ToList();
        var esperados = fase.NumeroConfrontos!.Value;
        if (confrontos.Count != esperados || confrontos.Select(c => c.Numero).Distinct().Count() != esperados
            || confrontos.Any(c => c.Numero < 1 || c.Numero > esperados))
            throw new DomainException($"defina os {esperados} confrontos (cruzamentos) antes de gerar a tabela");

        var jogos = new List<Jogo>();
        var porSerie = fase.JogosPorConfronto!.Value;
        var minimo = (porSerie + 1) / 2;

        foreach (var confronto in confrontos)
        {
            var a = new FaseEquipe(fase.Id, null, confronto.Numero * 2 - 1, confronto.OrigemA.Copiar());
            var b = new FaseEquipe(fase.Id, null, confronto.Numero * 2, confronto.OrigemB.Copiar());
            tabela.Vagas.Add(a);
            tabela.Vagas.Add(b);

            for (var k = 1; k <= porSerie; k++)
            {
                var (casa, visitante) = k % 2 == 1 ? (a, b) : (b, a);
                var jogo = new Jogo(temporadaId, fase.Id, null, confronto.Id, k, k, k > minimo, casa, visitante);
                // Desempate dentro da rodada: número do confronto (via Numero provisório).
                jogo.Numerar(confronto.Numero);
                jogos.Add(jogo);
            }
        }

        tabela.Jogos.AddRange(jogos);
        return jogos;
    }

    private static List<Jogo> GerarTodosContraTodos(Guid temporadaId, Fase fase, CategoriaParaGerar categoria,
        IReadOnlyDictionary<Guid, int> classificadosPorFase, TabelaGerada tabela)
    {
        var origens = OrigensDasVagas(fase, categoria, classificadosPorFase);
        if (origens.Count < 2)
            throw new DomainException("a fase precisa de ao menos 2 equipes");

        var jogos = new List<Jogo>();
        var turnos = fase.NumeroTurnos!.Value;

        if (fase.Tipo == TipoFase.PontosCorridos)
        {
            var vagas = origens.Select((o, i) => new FaseEquipe(fase.Id, null, i + 1, o)).ToList();
            tabela.Vagas.AddRange(vagas);
            jogos.AddRange(JogosDasRodadas(temporadaId, fase, null, vagas, turnos));
        }
        else
        {
            var grupos = fase.NumeroGrupos!.Value;
            if (origens.Count / grupos < 2)
                throw new DomainException($"{origens.Count} equipes não formam {grupos} grupos de pelo menos 2 equipes");

            var criados = Enumerable.Range(1, grupos).Select(o => new Grupo(fase.Id, o)).ToList();
            tabela.Grupos.AddRange(criados);

            var manual = fase.Distribuicao == DistribuicaoEquipes.Manual ? fase.GruposManual : null;
            if (manual is not null && manual.Count != origens.Count)
                throw new DomainException($"a distribuição manual dos grupos tem {manual.Count} vagas, mas a fase tem {origens.Count} — ajuste os grupos no detalhe da fase");

            var vagas = origens.Select((o, i) =>
                new FaseEquipe(fase.Id, criados[manual is not null ? manual[i] - 1 : IndiceDoGrupo(fase.Distribuicao!.Value, i, origens.Count, grupos)].Id, i + 1, o)).ToList();
            tabela.Vagas.AddRange(vagas);

            foreach (var grupo in criados)
                jogos.AddRange(JogosDasRodadas(temporadaId, fase, grupo.Id, vagas.Where(v => v.GrupoId == grupo.Id).ToList(), turnos));
        }

        tabela.Jogos.AddRange(jogos);
        return jogos;
    }

    /// <summary>Origem de cada vaga: equipes da categoria (1ª fase) ou os classificados da fase anterior.</summary>
    private static List<ReferenciaEquipe> OrigensDasVagas(Fase fase, CategoriaParaGerar categoria, IReadOnlyDictionary<Guid, int> classificadosPorFase)
    {
        if (fase.FaseAnteriorId is null)
            return categoria.Equipes
                .OrderBy(e => e.Nome, StringComparer.CurrentCultureIgnoreCase)
                .Select(e => ReferenciaEquipe.DeEquipe(e.Id))
                .ToList();

        var anterior = categoria.Fases.First(f => f.Id == fase.FaseAnteriorId);
        var quantos = classificadosPorFase.GetValueOrDefault(anterior.Id);

        if (anterior.Tipo == TipoFase.MataMata)
        {
            var confrontos = categoria.Confrontos.Where(c => c.FaseId == anterior.Id).OrderBy(c => c.Numero).ToList();
            return confrontos.Select(c => ReferenciaEquipe.DeVencedor(c.Id)).ToList();
        }

        return Enumerable.Range(1, quantos).Select(k => ReferenciaEquipe.DeColocacao(anterior.Id, null, k)).ToList();
    }

    /// <summary>Posição (0-based) → grupo (0-based). Serpentina: 1,4,5,8 / 2,3,6,7. Alternada: 1,3,5,7 / 2,4,6,8. Manual: blocos seguidos.</summary>
    public static int IndiceDoGrupo(DistribuicaoEquipes distribuicao, int posicao, int equipes, int grupos) => distribuicao switch
    {
        DistribuicaoEquipes.Serpentina => (posicao / grupos) % 2 == 0 ? posicao % grupos : grupos - 1 - posicao % grupos,
        DistribuicaoEquipes.Alternada => posicao % grupos,
        _ => posicao * grupos / equipes
    };

    private static IEnumerable<Jogo> JogosDasRodadas(Guid temporadaId, Fase fase, Guid? grupoId, IReadOnlyList<FaseEquipe> vagas, int turnos)
    {
        var rodadas = RodadasTodosContraTodos(vagas);
        for (var t = 0; t < turnos; t++)
        {
            for (var r = 0; r < rodadas.Count; r++)
            {
                foreach (var (a, b) in rodadas[r])
                {
                    // Turnos pares invertem o mando de campo.
                    var (casa, visitante) = t % 2 == 0 ? (a, b) : (b, a);
                    yield return new Jogo(temporadaId, fase.Id, grupoId, null, t * rodadas.Count + r + 1, null, false, casa, visitante);
                }
            }
        }
    }

    /// <summary>Método do círculo: n-1 rodadas (n par) em que cada dupla se enfrenta uma vez; com n ímpar, uma folga por rodada.</summary>
    public static List<List<(T Casa, T Visitante)>> RodadasTodosContraTodos<T>(IReadOnlyList<T> times) where T : class
    {
        var lista = new List<T?>(times);
        if (lista.Count % 2 == 1) lista.Add(null);

        var total = lista.Count;
        var rodadas = new List<List<(T, T)>>();
        for (var r = 0; r < total - 1; r++)
        {
            var jogos = new List<(T, T)>();
            for (var i = 0; i < total / 2; i++)
            {
                var a = lista[i];
                var b = lista[total - 1 - i];
                if (a is not null && b is not null) jogos.Add((a, b));
            }
            rodadas.Add(jogos);

            var ultimo = lista[^1];
            lista.RemoveAt(lista.Count - 1);
            lista.Insert(1, ultimo);
        }
        return rodadas;
    }
}
