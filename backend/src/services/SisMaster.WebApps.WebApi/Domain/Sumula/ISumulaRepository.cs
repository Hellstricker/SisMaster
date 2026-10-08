using SisMaster.Core.Data;

namespace SisMaster.WebApps.WebApi.Domain.Sumula;

public interface ISumulaRepository : IRepository<Sumula>
{
    /// <summary>Súmula com times, jogadores e eventos (rastreada).</summary>
    Task<Sumula?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<Sumula?> ObterPorJogoAsync(Guid jogoId, CancellationToken ct = default);

    Task<bool> ExisteParaJogoAsync(Guid jogoId, CancellationToken ct = default);

    void Adicionar(Sumula sumula);
    void Remover(Sumula sumula);
    void Atualizar(Sumula sumula);
    void AdicionarEvento(EventoSumula evento);
    void AdicionarSubstituicao(Substituicao substituicao);
    void RemoverSubstituicao(Substituicao substituicao);

    /// <summary>Outra súmula (que não a informada) já importou este jogo da fonte externa.</summary>
    Task<bool> ExisteOutraComCodigoExternoAsync(string codigoExterno, Guid sumulaId, CancellationToken ct = default);

    /// <summary>Guarda (ou substitui) o JSON bruto da fonte externa da súmula; grava junto com o próximo Commit.</summary>
    Task GuardarDadosExternosAsync(Guid sumulaId, string json, CancellationToken ct = default);

    /// <summary>O JSON bruto da importação, ou nulo se a súmula não veio de fonte externa.</summary>
    Task<string?> ObterDadosExternosAsync(Guid sumulaId, CancellationToken ct = default);

    // Eventos e trocas de uma importação: Add/Remove explícitos no DbSet (nunca por detecção de mudanças).
    void AplicarImportacao(ResultadoImportacao resultado);

    /// <summary>Súmulas dos jogos informados, com times, jogadores e substituições (rastreadas: refletem o que ainda não foi gravado).</summary>
    Task<IReadOnlyList<Sumula>> ListarPorJogosAsync(IReadOnlyCollection<Guid> jogoIds, CancellationToken ct = default);

    // Jogadores novos/removidos de uma súmula já carregada: Add/Remove explícitos no DbSet (nunca por detecção de mudanças).
    void AdicionarJogadores(IEnumerable<Jogador> jogadores);
    void RemoverJogadores(IEnumerable<Jogador> jogadores);
}
