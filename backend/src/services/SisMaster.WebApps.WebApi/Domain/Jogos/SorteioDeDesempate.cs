using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Jogos;

/// <summary>
/// Resultado do sorteio da diretoria para um empate que nenhum critério do regulamento resolveu, numa tabela da fase
/// (a fase inteira em pontos corridos, ou um grupo). Guarda a ordem das vagas empatadas; a classificação passa a usá-la
/// no último critério. As referências são só Guid (sem FK), como as origens das vagas.
/// </summary>
public class SorteioDeDesempate : Entity, IAggregateRoot
{
    public Guid FaseId { get; private set; }

    /// <summary>Grupo da tabela (nulo = a fase inteira, em pontos corridos).</summary>
    public Guid? GrupoId { get; private set; }

    /// <summary>Ids das vagas empatadas, na ordem sorteada (da melhor para a pior colocação), separados por vírgula.</summary>
    public string Ordem { get; private set; } = string.Empty;
    public DateTime DefinidoEm { get; private set; }

    public IReadOnlyList<Guid> OrdemDasVagas => Ordem.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();

    protected SorteioDeDesempate() { }

    public SorteioDeDesempate(Guid faseId, Guid? grupoId, IReadOnlyList<Guid> ordem)
    {
        FaseId = faseId;
        GrupoId = grupoId;
        Redefinir(ordem);
    }

    public void Redefinir(IReadOnlyList<Guid> ordem)
    {
        if (ordem.Count < 2) throw new DomainException("Um sorteio precisa de ao menos 2 equipes");
        if (ordem.Distinct().Count() != ordem.Count) throw new DomainException("O sorteio tem equipes repetidas");
        Ordem = string.Join(',', ordem);
        DefinidoEm = DateTime.UtcNow;
    }
}
