using SisMaster.Core.DomainObjects;
using SisMaster.WebApps.WebApi.Domain.Sumula.Enums;

namespace SisMaster.WebApps.WebApi.Domain.Sumula;

/// <summary>
/// A súmula de um jogo: a relação dos jogadores (dois <see cref="Time"/>), os eventos, o placar e as faltas, o período e o
/// status — mais o estado de operação da mesa (cronômetro, shot clock e posse). Ligada 1:1 ao Jogo do campeonato.
/// Ciclo: EmPreparacao (a relação ainda pode mudar) → EmAndamento → Intervalo → Encerrada.
/// </summary>
public class Sumula : Entity, IAggregateRoot
{
    public Guid JogoId { get; private set; }
    public Guid TimeCasaId { get; private set; }
    public Guid TimeVisitanteId { get; private set; }
    public StatusSumula Status { get; private set; }
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

    private readonly List<EventoSumula> _eventos = [];
    public IReadOnlyCollection<EventoSumula> Eventos => _eventos.AsReadOnly();

    private readonly List<Substituicao> _substituicoes = [];
    public IReadOnlyCollection<Substituicao> Substituicoes => _substituicoes.AsReadOnly();

    private readonly List<Time> _times = [];
    public IReadOnlyCollection<Time> Times => _times.AsReadOnly();

    protected Sumula() { }

    /// <summary>Cria a súmula de um jogo, com os dois times ainda sem relação (estado EmPreparacao).</summary>
    public Sumula(Guid jogoId, Guid equipeCasaId, string nomeCasa, string? corCasa, Guid equipeVisitanteId, string nomeVisitante, string? corVisitante)
    {
        if (equipeCasaId == equipeVisitanteId)
            throw new DomainException("Casa e visitante não podem ser a mesma equipe");

        JogoId = jogoId;
        var casa = new Time(Id, LadoTime.Casa, equipeCasaId, nomeCasa, corCasa);
        var visitante = new Time(Id, LadoTime.Visitante, equipeVisitanteId, nomeVisitante, corVisitante);
        _times.Add(casa);
        _times.Add(visitante);
        TimeCasaId = casa.Id;
        TimeVisitanteId = visitante.Id;

        Status = StatusSumula.EmPreparacao;
        PeriodoAtual = Periodo.Primeiro;
        CronometroSegundosRestantes = 600; // 10 minutos
        ShotClockSegundosRestantes = 24;
        PosseCasa = true;
    }

    public Time TimeDoLado(LadoTime lado) => _times.First(t => t.Lado == lado);

    public Jogador? ObterJogador(Guid jogadorId) => _times.SelectMany(t => t.Jogadores).FirstOrDefault(j => j.Id == jogadorId);

    // ---------- relação dos jogadores (só enquanto EmPreparacao) ----------

    /// <summary>
    /// Substitui a relação de um dos times (comissão, capitão e jogadores). Pode ser salva incompleta; só o início da súmula exige tudo.
    /// Devolve os jogadores que saíram e os que entraram, para o repositório aplicar explicitamente.
    /// </summary>
    public (List<Jogador> Removidos, List<Jogador> Adicionados) SalvarRelacao(
        LadoTime lado, string? tecnico, string? auxiliarTecnico, IReadOnlyList<RelacionadoNovo> relacionados, Guid? capitaoAtletaId)
    {
        if (Status != StatusSumula.EmPreparacao)
            throw new DomainException("A relação dos jogadores só pode mudar antes de a súmula ser iniciada");
        return TimeDoLado(lado).SubstituirRelacao(tecnico, auxiliarTecnico, relacionados, capitaoAtletaId);
    }

    /// <summary>Pendências para iniciar, por lado (vazio = pronto).</summary>
    public IReadOnlyDictionary<LadoTime, IReadOnlyList<string>> Pendencias() =>
        _times.ToDictionary(t => t.Lado, t => t.Pendencias());

    /// <summary>Inicia a súmula: exige as duas relações completas; a partir daqui a relação trava.</summary>
    public void Iniciar()
    {
        if (Status != StatusSumula.EmPreparacao)
            throw new DomainException("A súmula já foi iniciada");

        var faltas = _times.Where(t => t.Pendencias().Count > 0)
            .Select(t => $"{t.Nome}: {string.Join(", ", t.Pendencias())}").ToList();
        if (faltas.Count > 0)
            throw new DomainException($"Complete a relação antes de iniciar — {string.Join("; ", faltas)}");

        Status = StatusSumula.EmAndamento;
    }

    // ---------- operação da mesa ----------

    public void IniciarCronometro()
    {
        if (Status == StatusSumula.EmPreparacao)
            throw new DomainException("Inicie a súmula (com a relação dos jogadores) antes de começar o cronômetro");
        if (Status == StatusSumula.Encerrada)
            throw new DomainException("Súmula já encerrada");

        Status = StatusSumula.EmAndamento;
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
        Status = StatusSumula.Intervalo;
    }

    public void Encerrar()
    {
        if (Status == StatusSumula.EmPreparacao)
            throw new DomainException("A súmula ainda não foi iniciada");
        PausarCronometro();
        Status = StatusSumula.Encerrada;
    }

    public EventoSumula RegistrarEvento(Guid jogadorId, TipoEvento tipo, int tempoSegundos)
    {
        if (Status == StatusSumula.EmPreparacao)
            throw new DomainException("Inicie a súmula antes de registrar eventos");
        if (Status == StatusSumula.Encerrada)
            throw new DomainException("Não é possível registrar eventos em uma súmula encerrada");

        var jogador = ObterJogador(jogadorId) ?? throw new DomainException("O jogador não está relacionado nesta súmula");

        var evento = new EventoSumula(Id, jogadorId, tipo, PeriodoAtual, tempoSegundos);
        _eventos.Add(evento);

        AplicarEvento(evento, jogador.TimeId, 1);
        return evento;
    }

    public void DesfazerUltimoEvento()
    {
        if (!_eventos.Any())
            throw new DomainException("Não há eventos para desfazer");

        var ultimo = _eventos.Last();
        var jogador = ObterJogador(ultimo.JogadorId) ?? throw new DomainException("Jogador do último evento não encontrado");
        _eventos.Remove(ultimo);
        AplicarEvento(ultimo, jogador.TimeId, -1);
    }

    // ---------- quadra e substituições ----------

    /// <summary>Duração do período atual em segundos (10 min; 5 min na prorrogação).</summary>
    private int DuracaoDoPeriodo => PeriodoAtual == Periodo.Prorrogacao ? 300 : 600;

    /// <summary>O relógio do período atual ainda não correu: trocas feitas agora valem como início do período.</summary>
    public bool RelogioNoInicio => !CronometroAtivo && CronometroSegundosRestantes >= DuracaoDoPeriodo;

    /// <summary>
    /// Quantos dos 4 períodos normais já terminaram. Em andamento ou no intervalo, o período atual (ou o que acabou de
    /// começar) não conta; encerrada a súmula, o período em que ela terminou conta. A prorrogação nunca conta.
    /// </summary>
    public int PeriodosNormaisEncerrados => Math.Min(4, Status == StatusSumula.Encerrada ? (int)PeriodoAtual : (int)PeriodoAtual - 1);

    /// <summary>Jogadores em quadra agora (os titulares, com as substituições aplicadas).</summary>
    public IReadOnlyCollection<Guid> QuadraDo(Time time)
    {
        var quadra = time.Jogadores.Where(j => j.Titular).Select(j => j.Id).ToHashSet();
        foreach (var s in _substituicoes.OrderBy(s => s.Ordem))
        {
            if (s.JogadorSaiId is null || quadra.Remove(s.JogadorSaiId.Value)) quadra.Add(s.JogadorEntraId);
        }
        return quadra;
    }

    /// <summary>Rodízio de cada relacionado do time nos períodos normais já encerrados.</summary>
    public IReadOnlyList<RodizioDoJogador> RodizioDo(Time time) =>
        ServicoRodizio.Calcular(
            time.Jogadores.Select(j => new ParticipanteRodizio(j.Id, j.Titular, j.ChegouNoPeriodo, j.ChegouNoInicio)).ToList(),
            _substituicoes.OrderBy(s => s.Ordem).Select(s => new TrocaDeQuadra(s.JogadorSaiId, s.JogadorEntraId, (int)s.Periodo, s.NoInicio)).ToList(),
            PeriodosNormaisEncerrados);

    /// <summary>
    /// Acrescenta à súmula, depois do início do jogo, um atleta que chegou atrasado (no master isso é permitido).
    /// Ele entra no banco; para ir à quadra, registra-se uma substituição. Devolve o jogador para o repositório adicioná-lo.
    /// </summary>
    public Jogador AcrescentarJogador(LadoTime lado, RelacionadoNovo relacionado)
    {
        if (Status is not (StatusSumula.EmAndamento or StatusSumula.Intervalo))
            throw new DomainException(Status == StatusSumula.EmPreparacao
                ? "Enquanto a súmula está em preparação, a relação é editada normalmente"
                : "A súmula já foi encerrada");
        return TimeDoLado(lado).AcrescentarJogador(relacionado, (int)PeriodoAtual, RelogioNoInicio);
    }

    /// <summary>
    /// Substituição. <paramref name="jogadorSaiId"/> pode ser nulo quando a quadra tem menos de 5 jogadores
    /// (a equipe começou com 4 ou menos): o jogador só entra, completando a quadra.
    /// </summary>
    public Substituicao RegistrarSubstituicao(Guid? jogadorSaiId, Guid jogadorEntraId, int tempoJogoSegundos)
    {
        if (Status is not (StatusSumula.EmAndamento or StatusSumula.Intervalo))
            throw new DomainException("Só é possível substituir com a súmula em andamento ou no intervalo");

        var entra = ObterJogador(jogadorEntraId) ?? throw new DomainException("O jogador que entra não está relacionado nesta súmula");
        var time = _times.First(t => t.Id == entra.TimeId);
        var quadra = QuadraDo(time);

        if (jogadorSaiId is null)
        {
            if (quadra.Count >= RegrasDeRelacao.Titulares)
                throw new DomainException("A quadra está completa: informe quem sai");
        }
        else
        {
            var sai = ObterJogador(jogadorSaiId.Value) ?? throw new DomainException("O jogador que sai não está relacionado nesta súmula");
            if (sai.TimeId != entra.TimeId)
                throw new DomainException("A substituição deve ser entre jogadores do mesmo time");
            if (!quadra.Contains(sai.Id)) throw new DomainException($"{sai.Nome} não está em quadra");
        }

        if (quadra.Contains(entra.Id)) throw new DomainException($"{entra.Nome} já está em quadra");

        var substituicao = new Substituicao(Id, _substituicoes.Count + 1, jogadorSaiId, entra.Id, PeriodoAtual, tempoJogoSegundos, RelogioNoInicio);
        _substituicoes.Add(substituicao);
        return substituicao;
    }

    /// <summary>Desfaz a última substituição registrada (devolve-a para o repositório remover).</summary>
    public Substituicao DesfazerUltimaSubstituicao()
    {
        if (Status == StatusSumula.Encerrada)
            throw new DomainException("A súmula já foi encerrada");
        var ultima = _substituicoes.OrderBy(s => s.Ordem).LastOrDefault()
            ?? throw new DomainException("Não há substituições para desfazer");
        _substituicoes.Remove(ultima);
        return ultima;
    }


    // ---------- importação de um jogo já realizado ----------

    /// <summary>Código do jogo na fonte externa (FIBA LiveStats) de onde os dados foram importados; nulo = súmula da mesa.</summary>
    public string? CodigoExterno { get; private set; }
    public DateTime? ImportadaEm { get; private set; }

    /// <summary>
    /// Substitui todos os eventos e trocas pelos de um jogo já realizado e encerra a súmula. A relação (jogadores, titulares) não
    /// muda. As trocas são conferidas contra a quadra (os titulares com as trocas aplicadas); nada é alterado se algo não fecha.
    /// </summary>
    public ResultadoImportacao ImportarJogoRealizado(string codigoExterno, IReadOnlyList<EventoImportado> eventos, IReadOnlyList<TrocaImportada> trocas)
    {
        if (string.IsNullOrWhiteSpace(codigoExterno))
            throw new DomainException("Informe o código do jogo na fonte externa");

        // Confere tudo antes de mexer.
        foreach (var e in eventos)
        {
            if (ObterJogador(e.JogadorId) is null) throw new DomainException("Há um lance de jogador que não está relacionado nesta súmula");
            Validacoes.ValidarSeMenorQue(e.TempoJogoSegundos, 0, "Tempo de jogo não pode ser negativo");
        }

        var quadras = _times.ToDictionary(t => t.Id, t => t.Jogadores.Where(j => j.Titular).Select(j => j.Id).ToHashSet());
        foreach (var t in trocas)
        {
            var entra = ObterJogador(t.JogadorEntraId) ?? throw new DomainException("O jogador que entra não está relacionado nesta súmula");
            var quadra = quadras[entra.TimeId];
            if (t.JogadorSaiId is null)
            {
                if (quadra.Count >= RegrasDeRelacao.Titulares) throw new DomainException($"A quadra de {entra.Nome} já estava completa");
            }
            else
            {
                var sai = ObterJogador(t.JogadorSaiId.Value) ?? throw new DomainException("O jogador que sai não está relacionado nesta súmula");
                if (sai.TimeId != entra.TimeId) throw new DomainException("A substituição deve ser entre jogadores do mesmo time");
                if (!quadra.Remove(sai.Id)) throw new DomainException($"{sai.Nome} não estava em quadra");
            }
            if (!quadra.Add(entra.Id)) throw new DomainException($"{entra.Nome} já estava em quadra");
        }

        if (Status == StatusSumula.EmPreparacao) Iniciar(); // exige a relação completa

        var eventosRemovidos = _eventos.ToList();
        var trocasRemovidas = _substituicoes.ToList();
        _eventos.Clear();
        _substituicoes.Clear();
        PlacarCasa = PlacarVisitante = FaltasCasa = FaltasVisitante = 0;

        var eventosNovos = new List<EventoSumula>();
        foreach (var e in eventos)
        {
            var ev = new EventoSumula(Id, e.JogadorId, e.Tipo, e.Periodo, e.TempoJogoSegundos);
            _eventos.Add(ev);
            eventosNovos.Add(ev);
            AplicarEvento(ev, ObterJogador(e.JogadorId)!.TimeId, 1);
        }

        var trocasNovas = new List<Substituicao>();
        foreach (var t in trocas)
        {
            var duracao = t.Periodo == Periodo.Prorrogacao ? 300 : 600;
            var s = new Substituicao(Id, trocasNovas.Count + 1, t.JogadorSaiId, t.JogadorEntraId, t.Periodo, t.TempoJogoSegundos, t.TempoJogoSegundos >= duracao);
            _substituicoes.Add(s);
            trocasNovas.Add(s);
        }

        var teveProrrogacao = eventos.Any(e => e.Periodo == Periodo.Prorrogacao) || trocas.Any(t => t.Periodo == Periodo.Prorrogacao);
        PeriodoAtual = teveProrrogacao ? Periodo.Prorrogacao : Periodo.Quarto;
        CronometroAtivo = ShotClockAtivo = false;
        CronometroIniciadoEm = ShotClockIniciadoEm = null;
        CronometroSegundosRestantes = 0;
        ShotClockSegundosRestantes = 0;
        Status = StatusSumula.Encerrada;
        CodigoExterno = codigoExterno.Trim();
        ImportadaEm = DateTime.UtcNow;

        return new ResultadoImportacao(eventosRemovidos, trocasRemovidas, eventosNovos, trocasNovas);
    }
    private void AplicarEvento(EventoSumula ev, Guid timeId, int fator)
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
