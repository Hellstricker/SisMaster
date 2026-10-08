using SisMaster.Core.DomainObjects;

namespace SisMaster.WebApps.WebApi.Domain.Jogos;

/// <summary>
/// Jogo da tabela do campeonato. Pertence a um grupo (ou à fase inteira, em pontos corridos) ou a um confronto
/// de mata-mata, nunca aos dois. Casa/visitante são vagas da fase (<see cref="FaseEquipe"/>): a equipe real
/// aparece quando a origem da vaga termina. O número é único na temporada, compartilhado entre categorias.
/// Data, hora e local são definidos depois da geração.
/// </summary>
public class Jogo : Entity, IAggregateRoot
{
    public Guid TemporadaId { get; private set; }
    public Guid FaseId { get; private set; }
    public Guid? GrupoId { get; private set; }
    public Guid? ConfrontoId { get; private set; }

    /// <summary>Número do jogo na temporada (1..N).</summary>
    public int Numero { get; private set; }

    /// <summary>Rodada dentro da fase (turnos seguidos); no mata-mata, o número do jogo da série.</summary>
    public int Rodada { get; private set; }

    /// <summary>Mata-mata: jogo 1, 2, 3… da série.</summary>
    public int? JogoDaSerie { get; private set; }

    /// <summary>Jogo "se necessário" de uma série: só acontece se a série ainda não estiver decidida.</summary>
    public bool Opcional { get; private set; }

    public Guid CasaId { get; private set; }
    public Guid VisitanteId { get; private set; }

    public DateOnly? Data { get; private set; }
    public TimeOnly? Hora { get; private set; }
    public Guid? LocalId { get; private set; }

    /// <summary>A súmula do jogo (contexto Súmula), só por Id. Nasce quando a súmula é preparada.</summary>
    public Guid? SumulaId { get; private set; }

    /// <summary>Placar do W.O.: vitória por 20 × 0, sem bonificação (regulamento FBBM Art. 29; ABABAS Art. 12 §4º).</summary>
    public const int PlacarWO = 20;

    public StatusJogo Status { get; private set; }
    public int? PlacarCasa { get; private set; }
    public int? PlacarVisitante { get; private set; }

    public FaseEquipe Casa { get; private set; } = null!;
    public FaseEquipe Visitante { get; private set; } = null!;
    public Local? Local { get; private set; }

    protected Jogo() { }

    public Jogo(Guid temporadaId, Guid faseId, Guid? grupoId, Guid? confrontoId, int rodada, int? jogoDaSerie, bool opcional,
        FaseEquipe casa, FaseEquipe visitante)
    {
        if (casa.Id == visitante.Id) throw new DomainException("Casa e visitante devem ser vagas diferentes");

        TemporadaId = temporadaId;
        FaseId = faseId;
        GrupoId = grupoId;
        ConfrontoId = confrontoId;
        Rodada = rodada;
        JogoDaSerie = jogoDaSerie;
        Opcional = opcional;
        CasaId = casa.Id;
        VisitanteId = visitante.Id;
        Casa = casa;
        Visitante = visitante;
        Status = StatusJogo.Agendado;
    }

    internal void Numerar(int numero) => Numero = numero;

    /// <summary>A súmula foi preparada (a relação ainda pode mudar): o jogo continua Agendado.</summary>
    public void VincularSumula(Guid sumulaId)
    {
        if (Status != StatusJogo.Agendado)
            throw new DomainException("Só é possível preparar a súmula de um jogo agendado");
        SumulaId = sumulaId;
    }

    /// <summary>A súmula preparada foi descartada (ex.: W.O.).</summary>
    public void DesvincularSumula() => SumulaId = null;

    /// <summary>A súmula foi iniciada: o jogo passa a Em andamento.</summary>
    public void IniciarPelaSumula(Guid sumulaId)
    {
        if (Status != StatusJogo.Agendado)
            throw new DomainException("O jogo não está agendado");
        SumulaId = sumulaId;
        Status = StatusJogo.EmAndamento;
    }

    /// <summary>A súmula foi encerrada: o jogo recebe o placar final.</summary>
    public void EncerrarPelaSumula(int placarCasa, int placarVisitante)
    {
        if (Status != StatusJogo.EmAndamento)
            throw new DomainException("O jogo não está em andamento");
        PlacarCasa = placarCasa;
        PlacarVisitante = placarVisitante;
        Status = StatusJogo.Encerrado;
    }

    /// <summary>
    /// O jogo já foi realizado e a súmula veio de uma importação: o jogo fica Encerrado com o placar importado.
    /// Vale para jogo agendado, em andamento ou já encerrado (reimportação); W.O. e dispensado não têm súmula.
    /// </summary>
    public void ReceberPlacarImportado(Guid sumulaId, int placarCasa, int placarVisitante)
    {
        if (Status is StatusJogo.WO or StatusJogo.Dispensado)
            throw new DomainException("Um jogo de W.O. ou dispensado não tem súmula para importar");
        SumulaId = sumulaId;
        PlacarCasa = placarCasa;
        PlacarVisitante = placarVisitante;
        Status = StatusJogo.Encerrado;
    }

    /// <summary>Registra o W.O.: a equipe ausente perde por 20 × 0. Só antes de a súmula começar.</summary>
    public void RegistrarWO(bool casaAusente)
    {
        if (Status != StatusJogo.Agendado)
            throw new DomainException("O W.O. só pode ser registrado antes de o jogo começar");
        PlacarCasa = casaAusente ? 0 : PlacarWO;
        PlacarVisitante = casaAusente ? PlacarWO : 0;
        Status = StatusJogo.WO;
    }

    /// <summary>A série já foi decidida: o jogo "se necessário" não será disputado. Só jogo agendado; descarta a súmula preparada.</summary>
    public void Dispensar()
    {
        if (Status != StatusJogo.Agendado)
            throw new DomainException("Só um jogo agendado pode ser dispensado");
        SumulaId = null;
        Status = StatusJogo.Dispensado;
    }

    /// <summary>Define (ou limpa) data, hora e local. Só enquanto o jogo está agendado.</summary>
    public void Agendar(DateOnly? data, TimeOnly? hora, Guid? localId)
    {
        if (Status != StatusJogo.Agendado)
            throw new DomainException("Só é possível alterar data, hora e local de um jogo agendado");
        if (hora is not null && data is null)
            throw new DomainException("Informe a data do jogo junto com a hora");

        Data = data;
        Hora = hora;
        LocalId = localId;
    }
}
