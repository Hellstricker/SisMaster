namespace SisMaster.WebApps.WebApi.Hubs.Dtos;

public class EventoRegistradoDto
{
    public Guid EventoId { get; set; }
    public Guid PartidaId { get; set; }
    public Guid JogadorId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public int PeriodoAtual { get; set; }
    public int TempoJogoSegundos { get; set; }
    public int PlacarCasa { get; set; }
    public int PlacarVisitante { get; set; }
    public int FaltasCasa { get; set; }
    public int FaltasVisitante { get; set; }
}
