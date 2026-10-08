using SisMaster.Core.Data;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Domain.Equipes;

public interface IEquipeRepository : IRepository<Equipe>
{
    /// <summary>Equipe com Atletas e TemporadaCategoria.Temporada carregados (rastreada).</summary>
    Task<Equipe?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Equipes da temporada (somente leitura) com elenco, pessoa e pagamentos.</summary>
    Task<IReadOnlyList<Equipe>> ListarPorTemporadaAsync(Guid temporadaId, CancellationToken ct = default);

    /// <summary>Pedidos efetivados da temporada que ainda não estão em nenhuma equipe (somente leitura).</summary>
    Task<IReadOnlyList<InscricaoCategoria>> ListarEfetivadosSemEquipeAsync(Guid temporadaId, CancellationToken ct = default);

    /// <summary>Quantas equipes cada categoria da temporada tem (só as que têm equipe aparecem).</summary>
    Task<IReadOnlyDictionary<Guid, int>> ContarPorCategoriaAsync(Guid temporadaId, CancellationToken ct = default);

    /// <summary>Atletas do elenco da equipe com o nome da pessoa (somente leitura), por nome.</summary>
    Task<IReadOnlyList<AtletaDoElenco>> ObterElencoAsync(Guid equipeId, CancellationToken ct = default);

    Task<bool> ExisteNomeNaTemporadaAsync(Guid temporadaId, string nome, Guid? ignorarEquipeId, CancellationToken ct = default);
    Task<bool> AtletaExisteParaAsync(Guid inscricaoCategoriaId, CancellationToken ct = default);

    void Adicionar(Equipe equipe);
    void Remover(Equipe equipe);

    // Filhos novos/removidos de agregado já carregado: Add/Remove explícitos no DbSet.
    void AdicionarAtleta(Atleta atleta);
    void RemoverAtleta(Atleta atleta);
}

/// <summary>Um atleta do elenco para consulta (convocação da súmula).</summary>
public sealed record AtletaDoElenco(Guid AtletaId, string Nome);
