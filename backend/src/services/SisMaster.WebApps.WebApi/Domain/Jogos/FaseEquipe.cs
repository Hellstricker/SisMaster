using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Equipes;

namespace SisMaster.WebApps.WebApi.Domain.Jogos;

/// <summary>
/// Uma vaga de equipe numa fase (a "posição" 1..N). Nasce na geração da tabela de jogos com a sua
/// <see cref="Origem"/>; a <see cref="EquipeId"/> é preenchida quando a origem termina (ou já na geração,
/// se a origem é uma equipe fixa). Os jogos apontam para a vaga, então resolver a equipe aqui atualiza todos eles.
/// </summary>
public class FaseEquipe : Entity
{
    public Guid FaseId { get; private set; }
    public Guid? GrupoId { get; private set; }
    public int Posicao { get; private set; }
    public ReferenciaEquipe Origem { get; private set; } = null!;
    public Guid? EquipeId { get; private set; }

    public Equipe? Equipe { get; private set; }

    protected FaseEquipe() { }

    /// <summary>A origem terminou: a equipe real ocupa a vaga (e, por ela, todos os jogos da vaga).</summary>
    public void ResolverEquipe(Guid equipeId)
    {
        if (EquipeId is not null) throw new DomainException("A vaga já tem equipe definida");
        EquipeId = equipeId;
    }

    public FaseEquipe(Guid faseId, Guid? grupoId, int posicao, ReferenciaEquipe origem)
    {
        FaseId = faseId;
        GrupoId = grupoId;
        Posicao = posicao;
        Origem = origem;
        EquipeId = origem.Tipo == TipoReferencia.Equipe ? origem.EquipeId : null;
    }
}
