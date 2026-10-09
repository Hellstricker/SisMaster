namespace SisMaster.WebApps.Tests.Infra;

/// <summary>
/// Conexão com o SQL Server usado pelos testes que precisam de banco real (cada teste cria e apaga o próprio banco).
/// Nenhuma senha fica no código: vem de SISMASTER_TESTE_SQLSERVER (connection string sem o Database) ou, na falta dela,
/// de SA_PASSWORD (e DB_PORT, padrão 1433) em localhost. O scripts/verificar.ps1 já define isso apontando para um banco descartável.
/// </summary>
public static class BancoDeTeste
{
    public static string ComBanco(string nomeDoBanco)
    {
        var baseConexao = Environment.GetEnvironmentVariable("SISMASTER_TESTE_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConexao))
        {
            var senha = Environment.GetEnvironmentVariable("SA_PASSWORD");
            if (string.IsNullOrWhiteSpace(senha))
                throw new InvalidOperationException(
                    "Defina SISMASTER_TESTE_SQLSERVER (ou SA_PASSWORD) para os testes com banco, rode scripts/verificar.ps1, " +
                    "ou use SISMASTER_TESTE_SEM_BANCO=1 para dispensá-los.");
            var porta = Environment.GetEnvironmentVariable("DB_PORT") ?? "1433";
            baseConexao = $"Server=localhost,{porta};User Id=sa;Password={senha};TrustServerCertificate=True";
        }
        return $"{baseConexao.TrimEnd(';')};Database={nomeDoBanco}";
    }
}
