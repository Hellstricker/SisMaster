using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Participantes;

namespace SisMaster.WebApps.WebApi.Domain.Equipes;

/// <summary>
/// Vínculo entre um pedido de inscrição efetivado e uma equipe — é ele que faz da pessoa um atleta daquela
/// categoria/temporada. Uma pessoa que joga duas categorias tem dois atletas (um por pedido efetivado).
/// </summary>
public class Atleta : Entity
{
    public Guid EquipeId { get; private set; }
    public Guid InscricaoCategoriaId { get; private set; }

    public InscricaoCategoria InscricaoCategoria { get; private set; } = null!;

    protected Atleta() { }

    internal Atleta(Guid equipeId, Guid inscricaoCategoriaId)
    {
        EquipeId = equipeId;
        InscricaoCategoriaId = inscricaoCategoriaId;
    }
}
