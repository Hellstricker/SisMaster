namespace SisMaster.WebApps.WebApi.Hubs.Dtos;

public class EstadoPartidaDto
{
    public Guid PartidaId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int PeriodoAtual { get; set; }
    public int PlacarCasa { get; set; }
    public int PlacarVisitante { get; set; }
    public int FaltasCasa { get; set; }
    public int FaltasVisitante { get; set; }
    public CronometroDto Cronometro { get; set; } = null!;

    // Último evento para exibir na barra de desfazer
    public UltimoEventoDto? UltimoEvento { get; set; }
}

public class UltimoEventoDto
{
    public Guid EventoId { get; set; }
    public Guid JogadorId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int PeriodoAtual { get; set; }
    public int TempoJogoSegundos { get; set; }
}
