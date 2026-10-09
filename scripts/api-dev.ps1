<#
.SYNOPSIS
  Roda a API localmente (dotnet run) contra o SQL Server do Docker, lendo os segredos do .env.
.DESCRIPTION
  Use quando quiser depurar a API fora do Docker. Requer o servico sqlserver de pe
  (docker compose up -d sqlserver). Porta: http://localhost:5099
#>
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\lib.ps1"
Importar-DotEnv
if (-not $env:SA_PASSWORD) { throw 'Defina SA_PASSWORD no arquivo .env (copie de .env.example).' }
$porta = if ($env:DB_PORT) { $env:DB_PORT } else { '1433' }
$banco = if ($env:DB_NAME) { $env:DB_NAME } else { 'SisMaster' }
$env:ConnectionStrings__CampeonatoConnection = "Server=localhost,$porta;Database=$banco;User Id=sa;Password=$($env:SA_PASSWORD);MultipleActiveResultSets=true;TrustServerCertificate=True"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Push-Location $script:Raiz
try { dotnet run --project backend/src/services/SisMaster.WebApps.WebApi --urls http://localhost:5099 } finally { Pop-Location }
