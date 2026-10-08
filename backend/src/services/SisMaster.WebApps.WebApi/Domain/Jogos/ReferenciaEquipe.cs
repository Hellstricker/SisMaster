using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Jogos;

public enum TipoReferencia
{
    /// <summary>Equipe fixa.</summary>
    Equipe,

    /// <summary>Colocação numa fase: "1º do Grupo A" ou "3º classificado da fase".</summary>
    Colocacao,

    Vencedor,
    Perdedor
}

/// <summary>
/// De onde vem uma equipe numa fase ou num confronto. Enquanto a origem não termina, a tela mostra a
/// referência ("1º do Grupo A", "Vencedor — Quartas, confronto 1"); quando termina, a equipe real é
/// resolvida na <see cref="FaseEquipe"/>. Referências a fase/confronto são só Guid (sem FK) para evitar ciclos;
/// quem exclui fase/confronto confere se alguma referência aponta para ele.
/// </summary>
public sealed class ReferenciaEquipe
{
    public TipoReferencia Tipo { get; private set; }
    public Guid? EquipeId { get; private set; }

    /// <summary>Colocação: a fase de origem.</summary>
    public Guid? FaseId { get; private set; }

    /// <summary>Colocação em fase de grupos: o grupo (1 = A, 2 = B…). Nulo = classificação geral da fase.</summary>
    public int? GrupoOrdem { get; private set; }
    public int? Posicao { get; private set; }

    /// <summary>Vencedor/Perdedor: o confronto de origem.</summary>
    public Guid? ConfrontoOrigemId { get; private set; }

    private ReferenciaEquipe() { }

    public static ReferenciaEquipe DeEquipe(Guid equipeId) =>
        new() { Tipo = TipoReferencia.Equipe, EquipeId = equipeId };

    public static ReferenciaEquipe DeColocacao(Guid faseId, int? grupoOrdem, int posicao)
    {
        if (posicao < 1) throw new DomainException("A posição deve ser ao menos 1");
        if (grupoOrdem is < 1) throw new DomainException("Grupo inválido");
        return new() { Tipo = TipoReferencia.Colocacao, FaseId = faseId, GrupoOrdem = grupoOrdem, Posicao = posicao };
    }

    public static ReferenciaEquipe DeVencedor(Guid confrontoId) =>
        new() { Tipo = TipoReferencia.Vencedor, ConfrontoOrigemId = confrontoId };

    public static ReferenciaEquipe DePerdedor(Guid confrontoId) =>
        new() { Tipo = TipoReferencia.Perdedor, ConfrontoOrigemId = confrontoId };

    /// <summary>Cópia independente (cada dono de um tipo "owned" precisa da sua própria instância).</summary>
    public ReferenciaEquipe Copiar() => (ReferenciaEquipe)MemberwiseClone();
}
