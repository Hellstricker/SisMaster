using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Jogos;

/// <summary>Grupo de uma fase do tipo Grupos (A, B, C…). Criado na geração da tabela de jogos.</summary>
public class Grupo : Entity
{
    public Guid FaseId { get; private set; }

    /// <summary>1 = A, 2 = B…</summary>
    public int Ordem { get; private set; }
    public string Nome { get; private set; } = string.Empty;

    protected Grupo() { }

    public Grupo(Guid faseId, int ordem)
    {
        FaseId = faseId;
        Ordem = ordem;
        Nome = NomeDaOrdem(ordem);
    }

    public static string NomeDaOrdem(int ordem) => ((char)('A' + ordem - 1)).ToString();
}
