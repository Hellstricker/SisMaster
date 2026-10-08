using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Jogos;

/// <summary>
/// Confronto de uma fase de mata-mata: liga duas origens (colocação, vencedor/perdedor de outro confronto ou
/// equipe fixa). Montado na Tela 8 enquanto a tabela de jogos não foi gerada.
/// </summary>
public class Confronto : Entity
{
    public Guid FaseId { get; private set; }

    /// <summary>1..NumeroConfrontos da fase.</summary>
    public int Numero { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public ReferenciaEquipe OrigemA { get; private set; } = null!;
    public ReferenciaEquipe OrigemB { get; private set; } = null!;

    protected Confronto() { }

    public Confronto(Guid faseId, int numero, string? nome, ReferenciaEquipe origemA, ReferenciaEquipe origemB)
    {
        FaseId = faseId;
        Numero = numero;
        Definir(nome, origemA, origemB);
    }

    public void Definir(string? nome, ReferenciaEquipe origemA, ReferenciaEquipe origemB)
    {
        if (!string.IsNullOrWhiteSpace(nome))
            Validacoes.ValidarTamanho(nome.Trim(), 1, 60, "Nome do confronto deve ter até 60 caracteres");
        if (origemA.Tipo == TipoReferencia.Equipe && origemB.Tipo == TipoReferencia.Equipe && origemA.EquipeId == origemB.EquipeId)
            throw new DomainException("Um confronto precisa de duas equipes diferentes");

        Nome = string.IsNullOrWhiteSpace(nome) ? $"Confronto {Numero}" : nome.Trim();
        OrigemA = origemA;
        OrigemB = origemB;
    }
}
