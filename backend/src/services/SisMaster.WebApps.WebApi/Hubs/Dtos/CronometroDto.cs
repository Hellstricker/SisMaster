namespace SisMaster.WebApps.WebApi.Hubs.Dtos;

public class CronometroDto
{
    public Guid PartidaId { get; set; }
    public bool Ativo { get; set; }
    public int SegundosRestantes { get; set; }
    public long? IniciadoEmMs { get; set; }  // Unix timestamp em ms para cálculo no cliente
    public int SegundosAoIniciar { get; set; }
    public bool ShotClockAtivo { get; set; }
    public int ShotClockSegundos { get; set; }
    public long? ShotClockIniciadoEmMs { get; set; }
    public int ShotClockSegundosAoIniciar { get; set; }
    public bool PosseCasa { get; set; }
    public int PeriodoAtual { get; set; }
}
