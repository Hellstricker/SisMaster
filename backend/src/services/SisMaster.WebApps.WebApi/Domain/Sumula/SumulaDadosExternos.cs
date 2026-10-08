namespace SisMaster.WebApps.WebApi.Domain.Sumula;

/// <summary>
/// O dado bruto, como veio da fonte externa (JSON do FIBA LiveStats), de uma súmula importada. Fica na mesma tabela da súmula
/// (table splitting), mas só é lido quando alguém pede: a mesa carrega a súmula a cada lance e não precisa dessas centenas de KB.
/// </summary>
public class SumulaDadosExternos
{
    public Guid SumulaId { get; private set; }
    public string Json { get; private set; } = string.Empty;

    protected SumulaDadosExternos() { }

    public SumulaDadosExternos(Guid sumulaId, string json)
    {
        SumulaId = sumulaId;
        Json = json;
    }

    public void Substituir(string json) => Json = json;
}
