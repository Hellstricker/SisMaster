using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Fases;

/// <summary>
/// Regras da sequência de fases de uma categoria: ordem 1..N contígua, reordenação e a regra de que a
/// "fase anterior" de uma fase precisa vir antes dela na sequência.
/// </summary>
public static class SequenciaDeFases
{
    public static List<Fase> Ordenar(IEnumerable<Fase> fases) => fases.OrderBy(f => f.Ordem).ToList();

    public static int ProximaOrdem(IReadOnlyCollection<Fase> fases) => fases.Count == 0 ? 1 : fases.Max(f => f.Ordem) + 1;

    /// <summary>A fase anterior informada existe na categoria (e ainda não é a própria fase).</summary>
    public static Fase ObterAnterior(IEnumerable<Fase> fasesDaCategoria, Guid faseAnteriorId, Guid? faseAtualId)
    {
        if (faseAtualId == faseAnteriorId)
            throw new DomainException("Uma fase não pode vir dela mesma");
        return fasesDaCategoria.FirstOrDefault(f => f.Id == faseAnteriorId)
            ?? throw new DomainException("A fase anterior precisa ser da mesma categoria");
    }

    /// <summary>Move a fase uma posição (-1 sobe, +1 desce). Recusa se a sequência passar a violar "anterior antes".</summary>
    public static void Mover(IEnumerable<Fase> fasesDaCategoria, Guid faseId, int direcao)
    {
        if (direcao is not (-1 or 1))
            throw new DomainException("Direção inválida");

        var ordenadas = Ordenar(fasesDaCategoria);
        var i = ordenadas.FindIndex(f => f.Id == faseId);
        if (i < 0) throw new DomainException("Fase não encontrada na categoria");

        var j = i + direcao;
        if (j < 0 || j >= ordenadas.Count)
            throw new DomainException(direcao < 0 ? "A fase já é a primeira da sequência" : "A fase já é a última da sequência");

        (ordenadas[i], ordenadas[j]) = (ordenadas[j], ordenadas[i]);
        ValidarAnteriores(ordenadas);
        Aplicar(ordenadas);
    }

    /// <summary>Remove a fase da sequência e renumera as demais (1..N).</summary>
    public static void Renumerar(IEnumerable<Fase> fasesRestantes) => Aplicar(Ordenar(fasesRestantes));

    public static void ValidarAnteriores(IReadOnlyList<Fase> ordenadas)
    {
        for (var i = 0; i < ordenadas.Count; i++)
        {
            var anteriorId = ordenadas[i].FaseAnteriorId;
            if (anteriorId is null) continue;

            var posicaoAnterior = -1;
            for (var k = 0; k < ordenadas.Count; k++)
                if (ordenadas[k].Id == anteriorId) { posicaoAnterior = k; break; }

            if (posicaoAnterior >= i)
            {
                var anterior = ordenadas.First(f => f.Id == anteriorId);
                throw new DomainException(
                    $"Não é possível mover: “{ordenadas[i].Nome}” vem de “{anterior.Nome}”, que precisa ficar antes dela na sequência");
            }
        }
    }

    private static void Aplicar(IReadOnlyList<Fase> ordenadas)
    {
        for (var i = 0; i < ordenadas.Count; i++)
            ordenadas[i].DefinirOrdem(i + 1);
    }
}
