using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Partida.Enums;

namespace SisMaster.WebApps.WebApi.Domain.Partida;

public class EventoPartida : Entity
{
    public Guid PartidaId { get; private set; }
    public Guid JogadorId { get; private set; }
    public TipoEvento Tipo { get; private set; }
    public Periodo Periodo { get; private set; }
    public int TempoJogoSegundos { get; private set; }
    public DateTime RegistradoEm { get; private set; }

    public Partida Partida { get; private set; } = null!;

    protected EventoPartida() { }

    public EventoPartida(Guid partidaId, Guid jogadorId, TipoEvento tipo, Periodo periodo, int tempoJogoSegundos)
    {
        Validacoes.ValidarSeMenorQue(tempoJogoSegundos, 0, "Tempo de jogo não pode ser negativo");

        PartidaId = partidaId;
        JogadorId = jogadorId;
        Tipo = tipo;
        Periodo = periodo;
        TempoJogoSegundos = tempoJogoSegundos;
        RegistradoEm = DateTime.UtcNow;
    }

    public bool GeraEntrada =>
        Tipo is TipoEvento.Ponto2 or TipoEvento.Ponto3 or TipoEvento.LanceLivre;

    public int PontosGerados => Tipo switch
    {
        TipoEvento.Ponto2 => 2,
        TipoEvento.Ponto3 => 3,
        TipoEvento.LanceLivre => 1,
        _ => 0
    };
}
