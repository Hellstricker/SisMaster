using System.Text.Json;
using System.Text.Json.Serialization;

namespace SisMaster.WebApps.WebApi.Application.Importacao.Fiba;

/// <summary>Feed público do FIBA LiveStats (data.json) — só os campos que a importação usa.</summary>
public sealed class FibaJogoDto
{
    [JsonPropertyName("tm")] public Dictionary<string, FibaTimeDto> Tm { get; set; } = [];
    [JsonPropertyName("pbp")] public List<FibaEventoDto> Pbp { get; set; } = [];
    [JsonPropertyName("periodsMax")] public int PeriodsMax { get; set; }
    [JsonPropertyName("periodLengthREGULAR")] public int PeriodLengthRegular { get; set; }
    [JsonPropertyName("periodLengthOVERTIME")] public int PeriodLengthOvertime { get; set; }
}

public sealed class FibaTimeDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("score")] public int Score { get; set; }
    [JsonPropertyName("pl")] public Dictionary<string, FibaJogadorDto> Pl { get; set; } = [];

    /// <summary>Campos soltos do time, como p1_score…p4_score (placar do período).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extras { get; set; }

    public int? PlacarDoPeriodo(int periodo) =>
        Extras is not null && Extras.TryGetValue($"p{periodo}_score", out var v) && v.TryGetInt32(out var n) ? n : null;
}

public sealed class FibaJogadorDto
{
    [JsonPropertyName("shirtNumber")] public string ShirtNumber { get; set; } = string.Empty;
    [JsonPropertyName("starter")] public int Starter { get; set; }
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    [JsonPropertyName("familyName")] public string? FamilyName { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("sPoints")] public int SPoints { get; set; }
    [JsonPropertyName("sFoulsPersonal")] public int SFoulsPersonal { get; set; }
    [JsonPropertyName("sReboundsOffensive")] public int SReboundsOffensive { get; set; }
    [JsonPropertyName("sReboundsDefensive")] public int SReboundsDefensive { get; set; }
    [JsonPropertyName("sAssists")] public int SAssists { get; set; }
    [JsonPropertyName("sSteals")] public int SSteals { get; set; }
    [JsonPropertyName("sBlocks")] public int SBlocks { get; set; }
    [JsonPropertyName("sTurnovers")] public int STurnovers { get; set; }

    /// <summary>Nome completo (o campo "name" do feed vem abreviado, ex.: "C. Melo").</summary>
    public string NomeCompleto =>
        string.IsNullOrWhiteSpace(FamilyName) ? Name ?? string.Empty : $"{FirstName} {FamilyName}".Trim();
}

public sealed class FibaEventoDto
{
    [JsonPropertyName("actionNumber")] public int ActionNumber { get; set; }
    [JsonPropertyName("period")] public int Period { get; set; }
    [JsonPropertyName("periodType")] public string PeriodType { get; set; } = string.Empty;
    /// <summary>Tempo que falta no período (mm:ss).</summary>
    [JsonPropertyName("gt")] public string Gt { get; set; } = "00:00";
    /// <summary>0 = evento neutro (início/fim), 1 e 2 = os times do feed.</summary>
    [JsonPropertyName("tno")] public int Tno { get; set; }
    [JsonPropertyName("shirtNumber")] public string? ShirtNumber { get; set; }
    [JsonPropertyName("player")] public string? Player { get; set; }
    [JsonPropertyName("actionType")] public string ActionType { get; set; } = string.Empty;
    [JsonPropertyName("subType")] public string? SubType { get; set; }
    [JsonPropertyName("success")] public int Success { get; set; }
}
