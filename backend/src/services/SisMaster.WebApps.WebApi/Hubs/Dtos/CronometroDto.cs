namespace SisMaster.WebApps.WebApi.Hubs.Dtos;

public class CronometroDto
{
    public Guid PartidaId { get; set; }
    public bool Ativo { get; set; }
    public double SegundosRestantes { get; set; }
    public long? IniciadoEmMs { get; set; }  // Unix timestamp em ms para cálculo no cliente
    public double SegundosAoIniciar { get; set; }
    public bool ShotClockAtivo { get; set; }
    public double ShotClockSegundos { get; set; }
    public long? ShotClockIniciadoEmMs { get; set; }
    public double ShotClockSegundosAoIniciar { get; set; }
    public bool PosseCasa { get; set; }
    public int PeriodoAtual { get; set; }

    /// <summary>Faltas de equipe do período atual, no máximo 5 (zeram quando o período muda).</summary>
    public int FaltasCasa { get; set; }
    public int FaltasVisitante { get; set; }

    /// <summary>A hora do servidor (Unix, ms) no momento do envio: o cliente corrige o relógio do próprio aparelho por ela.</summary>
    public long AgoraMs { get; set; }
}
