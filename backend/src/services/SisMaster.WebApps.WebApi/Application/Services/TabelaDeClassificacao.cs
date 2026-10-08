using SisMaster.WebApps.WebApi.Domain.Jogos;

namespace SisMaster.WebApps.WebApi.Application.Services;

/// <summary>Classificação de uma tabela (fase de pontos corridos ou um grupo) a partir dos jogos já gravados.</summary>
public static class TabelaDeClassificacao
{
    public static bool Concluido(Jogo j) =>
        j.Status is StatusJogo.Encerrado or StatusJogo.WO && j.PlacarCasa is not null && j.PlacarVisitante is not null;

    /// <summary>Todos os jogos terminaram (ou foram dispensados): a ordem final já pode ser conhecida.</summary>
    public static bool Definitiva(IEnumerable<Jogo> jogos)
    {
        var lista = jogos.ToList();
        return lista.Count > 0 && lista.All(j => Concluido(j) || j.Status == StatusJogo.Dispensado);
    }

    public static ResultadoJogo? Resultado(Jogo j, IReadOnlyDictionary<Guid, BonificacaoDoJogo>? bonificacoes = null)
    {
        if (!Concluido(j)) return null;
        var b = bonificacoes is not null && bonificacoes.TryGetValue(j.Id, out var x) ? x : default;
        return new ResultadoJogo(j.CasaId, j.VisitanteId, j.PlacarCasa!.Value, j.PlacarVisitante!.Value, j.Status == StatusJogo.WO, b.Casa, b.Visitante);
    }

    public static List<LinhaClassificacao> Calcular(IReadOnlyCollection<FaseEquipe> vagas, IReadOnlyCollection<Jogo> jogos,
        Func<FaseEquipe, string> nomeDaVaga, IReadOnlyDictionary<Guid, BonificacaoDoJogo>? bonificacoes, IReadOnlyList<Guid>? ordemDoSorteio)
    {
        var resultados = jogos.OrderBy(j => j.Numero).Select(j => Resultado(j, bonificacoes)).Where(r => r is not null).Select(r => r!).ToList();
        return ServicoClassificacao.Classificar(vagas.Select(v => new Participante(v.Id, nomeDaVaga(v))).ToList(), resultados, ordemDoSorteio).ToList();
    }
}
