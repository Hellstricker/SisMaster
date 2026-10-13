namespace SisMaster.WebApps.WebApi.Application.Services;

/// <summary>
/// A hora do lugar onde a associação joga (por padrão a Bahia, America/Bahia), independente do fuso do servidor. O servidor roda em UTC
/// (Docker): sem isto, depois das 21h em Salvador o sistema já "viraria o dia" e mostraria os jogos de hoje, as inscrições abertas e as
/// janelas de datas com um dia de diferença. O fuso pode ser trocado pela variável de ambiente FUSO_HORARIO.
/// </summary>
public static class RelogioLocal
{
    private static readonly TimeZoneInfo Fuso = Resolver();

    private static TimeZoneInfo Resolver()
    {
        var id = Environment.GetEnvironmentVariable("FUSO_HORARIO") ?? "America/Bahia";
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Servidor sem a base de fusos: usa o deslocamento fixo da Bahia (UTC−3, sem horário de verão).
            return TimeZoneInfo.CreateCustomTimeZone("Bahia", TimeSpan.FromHours(-3), "Bahia", "Bahia");
        }
    }

    public static DateTime Agora => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Fuso);
    /// <summary>Converte um instante gravado em UTC (o banco guarda UtcNow) para a hora local, para mostrar à pessoa.</summary>
    public static DateTime DeUtc(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Fuso);

    public static DateTime? DeUtc(DateTime? utc) => utc is null ? null : DeUtc(utc.Value);

    public static DateOnly Hoje => DateOnly.FromDateTime(Agora);
}
