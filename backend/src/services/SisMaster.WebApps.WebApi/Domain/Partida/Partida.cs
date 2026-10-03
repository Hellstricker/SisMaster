using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Partida.Enums;

namespace SisMaster.WebApps.WebApi.Domain.Partida;

public class Partida : Entity, SisMaster.Core.DomainObjects.IAggregateRoot
{
    public Guid CampeonatoId { get; private set; }
    public Guid TimeCasaId { get; private set; }
    public Guid TimeVisitanteId { get; private set; }
    public StatusPartida Status { get; private set; }
    public Periodo PeriodoAtual { get; private set; }
    public int PlacarCasa { get; private set; }
    public int PlacarVisitante { get; private set; }
    public int FaltasCasa { get; private set; }
    public int FaltasVisitante { get; private set; }

    // Cronômetro: armazenados para reconstrução do estado
    public bool CronometroAtivo { get; private set; }
    public int CronometroSegundosRestantes { get; private set; }
    public DateTime? CronometroIniciadoEm { get; private set; }
    public int CronometroSegundosAoIniciar { get; private set; }

    // Shot clock
    public bool ShotClockAtivo { get; private set; }
    public int ShotClockSegundosRestantes { get; private set; }
    public DateTime? ShotClockIniciadoEm { get; private set; }
    public int ShotClockSegundosAoIniciar { get; private set; }

    // Posse
    public bool PosseCasa { get; private set; }

    private List<EventoPartida> _eventos = [];
    public IReadOnlyCollection<EventoPartida> Eventos => _eventos.AsReadOnly();

    protected Partida() { }

    public Partida(Guid campeonatoId, Guid timeCasaId, Guid timeVisitanteId)
    {
        Validacoes.ValidarSeDiferente(timeCasaId, timeVisitanteId, "Casa e visitante não podem ser o mesmo time");

        CampeonatoId = campeonatoId;
        TimeCasaId = timeCasaId;
        TimeVisitanteId = timeVisitanteId;
        Status = StatusPartida.NaoIniciada;
        PeriodoAtual = Periodo.Primeiro;
        CronometroSegundosRestantes = 600; // 10 minutos
        ShotClockSegundosRestantes = 24;
        PosseCasa = true;
    }

    public void IniciarCronometro()
    {
        if (Status == StatusPartida.Encerrada)
            throw new DomainException("Partida já encerrada");

        Status = StatusPartida.EmAndamento;
        CronometroAtivo = true;
        CronometroIniciadoEm = DateTime.UtcNow;
        CronometroSegundosAoIniciar = CronometroSegundosRestantes;

        if (!ShotClockAtivo)
        {
            ShotClockAtivo = true;
            ShotClockIniciadoEm = DateTime.UtcNow;
            ShotClockSegundosAoIniciar = ShotClockSegundosRestantes;
        }
    }

    public void PausarCronometro()
    {
        if (!CronometroAtivo) return;

        CronometroSegundosRestantes = CalcularSegundosRestantesCronometro();
        CronometroAtivo = false;
        CronometroIniciadoEm = null;

        ShotClockSegundosRestantes = CalcularSegundosRestantesShotClock();
        ShotClockAtivo = false;
        ShotClockIniciadoEm = null;
    }

    public void ResetarShotClock(int segundos = 24)
    {
        Validacoes.ValidarMinimoMaximo(segundos, 1, 24, "Shot clock deve estar entre 1 e 24 segundos");

        ShotClockSegundosRestantes = segundos;
        if (CronometroAtivo)
        {
            ShotClockAtivo = true;
            ShotClockIniciadoEm = DateTime.UtcNow;
            ShotClockSegundosAoIniciar = segundos;
        }
    }

    public void AlternarPosse()
    {
        PosseCasa = !PosseCasa;
        ResetarShotClock(24);
    }

    public void AvancarPeriodo()
    {
        PausarCronometro();
        PeriodoAtual = PeriodoAtual switch
        {
            Periodo.Primeiro => Periodo.Segundo,
            Periodo.Segundo => Periodo.Terceiro,
            Periodo.Terceiro => Periodo.Quarto,
            Periodo.Quarto => Periodo.Prorrogacao,
            _ => throw new DomainException("Não é possível avançar além da prorrogação")
        };
        CronometroSegundosRestantes = PeriodoAtual == Periodo.Prorrogacao ? 300 : 600;
        ShotClockSegundosRestantes = 24;
        Status = StatusPartida.Intervalo;
    }

    public void Encerrar()
    {
        PausarCronometro();
        Status = StatusPartida.Encerrada;
    }

    public EventoPartida RegistrarEvento(Guid jogadorId, Guid timeId, TipoEvento tipo, int tempoSegundos)
    {
        if (Status == StatusPartida.Encerrada)
            throw new DomainException("Não é possível registrar eventos em uma partida encerrada");

        var evento = new EventoPartida(Id, jogadorId, tipo, PeriodoAtual, tempoSegundos);
        _eventos.Add(evento);

        AplicarEvento(evento, timeId, 1);
        return evento;
    }

    public void DesfazerUltimoEvento(Guid timeIdUltimoEvento)
    {
        if (!_eventos.Any())
            throw new DomainException("Não há eventos para desfazer");

        var ultimo = _eventos.Last();
        _eventos.Remove(ultimo);
        AplicarEvento(ultimo, timeIdUltimoEvento, -1);
    }

    private void AplicarEvento(EventoPartida ev, Guid timeId, int fator)
    {
        var ehCasa = timeId == TimeCasaId;

        switch (ev.Tipo)
        {
            case TipoEvento.Ponto2:
            case TipoEvento.Ponto3:
            case TipoEvento.LanceLivre:
                if (ehCasa) PlacarCasa += ev.PontosGerados * fator;
                else PlacarVisitante += ev.PontosGerados * fator;
                break;
            case TipoEvento.FaltaPessoal:
            case TipoEvento.FaltaTecnica:
            case TipoEvento.FaltaAntiDesportiva:
                if (ehCasa) FaltasCasa += fator;
                else FaltasVisitante += fator;
                break;
        }

        PlacarCasa = Math.Max(0, PlacarCasa);
        PlacarVisitante = Math.Max(0, PlacarVisitante);
        FaltasCasa = Math.Max(0, FaltasCasa);
        FaltasVisitante = Math.Max(0, FaltasVisitante);
    }

    private int CalcularSegundosRestantesCronometro()
    {
        if (!CronometroAtivo || CronometroIniciadoEm is null) return CronometroSegundosRestantes;
        var decorrido = (int)(DateTime.UtcNow - CronometroIniciadoEm.Value).TotalSeconds;
        return Math.Max(0, CronometroSegundosAoIniciar - decorrido);
    }

    private int CalcularSegundosRestantesShotClock()
    {
        if (!ShotClockAtivo || ShotClockIniciadoEm is null) return ShotClockSegundosRestantes;
        var decorrido = (int)(DateTime.UtcNow - ShotClockIniciadoEm.Value).TotalSeconds;
        return Math.Max(0, ShotClockSegundosAoIniciar - decorrido);
    }
}
