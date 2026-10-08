using System.Text.Json;

namespace SisMaster.WebApps.WebApi.Application.Importacao.Fiba;

/// <summary>O feed de um jogo: o texto JSON como veio (para guardar) e o que foi lido dele.</summary>
public sealed record FibaFeed(FibaJogoDto Jogo, string Json);

public interface IFibaLiveStatsClient
{
    Task<FibaFeed> ObterJogoAsync(string codigoJogo, CancellationToken ct = default);
}

public sealed class FibaLiveStatsClient : IFibaLiveStatsClient
{
    private static readonly JsonSerializerOptions Opcoes = new() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };

    private readonly HttpClient _http;

    public FibaLiveStatsClient(HttpClient http) => _http = http;

    public async Task<FibaFeed> ObterJogoAsync(string codigoJogo, CancellationToken ct = default)
    {
        var json = await _http.GetStringAsync($"data/{Uri.EscapeDataString(codigoJogo)}/data.json", ct);
        var jogo = JsonSerializer.Deserialize<FibaJogoDto>(json, Opcoes);
        return jogo is null ? throw new InvalidOperationException($"O feed do jogo '{codigoJogo}' veio vazio") : new FibaFeed(jogo, json);
    }

    /// <summary>O código do jogo no LiveStats (ex.: 2888968): só dígitos, até 12; qualquer outra coisa é inválida (nulo).</summary>
    public static string? NormalizarCodigo(string? entrada)
    {
        var texto = entrada?.Trim();
        return !string.IsNullOrEmpty(texto) && texto.Length <= 12 && texto.All(char.IsAsciiDigit) ? texto : null;
    }
}
