using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Jogos;

public interface IJogoRepository : IRepository<Jogo>
{
    /// <summary>Jogo rastreado, com as vagas casa/visitante (a fase/temporada se obtém pelo repositório de fases).</summary>
    Task<Jogo?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Confrontos da fase (rastreados), por número.</summary>
    Task<List<Confronto>> ListarConfrontosDaFaseAsync(Guid faseId, CancellationToken ct = default);

    /// <summary>Confrontos de todas as fases da categoria (somente leitura).</summary>
    Task<IReadOnlyList<Confronto>> ListarConfrontosDaCategoriaAsync(Guid temporadaCategoriaId, CancellationToken ct = default);

    /// <summary>Confrontos de todas as fases da temporada (somente leitura).</summary>
    Task<IReadOnlyList<Confronto>> ListarConfrontosDaTemporadaAsync(Guid temporadaId, CancellationToken ct = default);

    /// <summary>Algum confronto usa a fase como origem de colocação?</summary>
    Task<bool> ExisteReferenciaAFaseAsync(Guid faseId, CancellationToken ct = default);

    /// <summary>Algum confronto usa estes confrontos como origem de vencedor/perdedor?</summary>
    Task<bool> ExisteReferenciaAosConfrontosAsync(IReadOnlyCollection<Guid> confrontoIds, CancellationToken ct = default);

    /// <summary>Sorteios de desempate registrados nas fases da temporada.</summary>
    Task<IReadOnlyList<SorteioDeDesempate>> ListarSorteiosDaTemporadaAsync(Guid temporadaId, CancellationToken ct = default);

    /// <summary>Sorteios de desempate da fase (somente leitura).</summary>
    Task<IReadOnlyList<SorteioDeDesempate>> ListarSorteiosDaFaseAsync(Guid faseId, CancellationToken ct = default);

    /// <summary>Sorteio da tabela (fase inteira ou grupo), rastreado.</summary>
    Task<SorteioDeDesempate?> ObterSorteioAsync(Guid faseId, Guid? grupoId, CancellationToken ct = default);

    void AdicionarSorteio(SorteioDeDesempate sorteio);

    /// <summary>Alguma vaga de outra fase já foi resolvida a partir da colocação nesta fase?</summary>
    Task<bool> ExisteVagaResolvidaPelaColocacaoAsync(Guid faseId, CancellationToken ct = default);

    /// <summary>
    /// Jogos já agendados (com data) que ainda não terminaram (Agendado ou Em andamento), de temporadas não encerradas,
    /// com vagas e local (somente leitura), por data e hora.
    /// </summary>
    Task<IReadOnlyList<Jogo>> ListarAgendadosPendentesAsync(CancellationToken ct = default);

    /// <summary>Quantos jogos usam o local.</summary>
    Task<int> ContarJogosNoLocalAsync(Guid localId, CancellationToken ct = default);

    Task<bool> ExistemJogosNaTemporadaAsync(Guid temporadaId, CancellationToken ct = default);

    /// <summary>Status dos jogos de cada fase da temporada (para derivar o status da fase).</summary>
    Task<IReadOnlyDictionary<Guid, List<StatusJogo>>> StatusPorFaseAsync(Guid temporadaId, CancellationToken ct = default);

    /// <summary>Grupos, vagas e jogos gerados da fase (somente leitura), com equipes e locais.</summary>
    Task<TabelaGerada> ObterTabelaDaFaseAsync(Guid faseId, CancellationToken ct = default);

    /// <summary>Todos os jogos da temporada (somente leitura), com vagas, equipes e local, por número.</summary>
    Task<IReadOnlyList<Jogo>> ListarPorTemporadaAsync(Guid temporadaId, CancellationToken ct = default);

    /// <summary>Jogos (rastreados) da temporada com os ids informados, por número.</summary>
    Task<List<Jogo>> ListarPorIdsAsync(Guid temporadaId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Fases, grupos, vagas, confrontos e jogos da temporada, todos rastreados (para o avanço automático).</summary>
    Task<DadosDaTemporada> CarregarParaAvancoAsync(Guid temporadaId, CancellationToken ct = default);

    void AdicionarConfronto(Confronto confronto);
    void RemoverConfronto(Confronto confronto);
    void AdicionarTabela(TabelaGerada tabela);
}

public sealed record DadosDaTemporada(IReadOnlyList<Fases.Fase> Fases, IReadOnlyList<Grupo> Grupos, IReadOnlyList<FaseEquipe> Vagas,
    IReadOnlyList<Confronto> Confrontos, IReadOnlyList<Jogo> Jogos,
    IReadOnlyList<SorteioDeDesempate> Sorteios);

public interface ILocalRepository : IRepository<Local>
{
    Task<Local?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Local>> ListarPorAssociacaoAsync(Guid associacaoId, CancellationToken ct = default);
    Task<bool> ExisteAsync(Guid associacaoId, string nome, string cidade, Guid? ignorarId, CancellationToken ct = default);
    void Adicionar(Local local);
    void Remover(Local local);

    /// <summary>Locais da associação com a quantidade de jogos que usam cada um.</summary>
    Task<IReadOnlyDictionary<Guid, int>> ContarUsosAsync(Guid associacaoId, CancellationToken ct = default);
}
