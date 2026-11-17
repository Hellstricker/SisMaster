namespace SisMaster.WebApps.WebApi.Seguranca;

/// <summary>Tipos de recurso que pertencem (direta ou indiretamente) a uma associação.</summary>
public enum Recurso
{
    Associacao,
    Campeonato,
    Temporada,
    TemporadaCategoria,
    Categoria,
    Inscricao,
    InscricaoCategoria,
    InscricaoDeEquipe,
    Equipe,
    Fase,
    Jogo,
    Sumula,
    Local
}

/// <summary>
/// Diz o que o parâmetro de rota <c>id</c> significa na rota (ou no controller inteiro). Os parâmetros com nome próprio
/// (temporadaId, jogoId...) e as propriedades dos comandos (TemporadaId, JogoId...) são reconhecidos pelo nome, sem atributo.
/// O filtro <see cref="EscopoDeAssociacaoFilter"/> confere se o recurso é de uma associação a que o usuário tem acesso.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IdEhAttribute(Recurso recurso) : Attribute
{
    public Recurso Recurso { get; } = recurso;
}
