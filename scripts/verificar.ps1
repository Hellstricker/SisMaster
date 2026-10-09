<#
.SYNOPSIS
  Verifica o projeto de ponta a ponta: build, testes (inclusive os que usam banco), front e subida do zero no Docker.
.DESCRIPTION
  Sobe um ambiente ISOLADO (projeto Docker sismaster-teste, banco vazio, portas 3300/5400/1634, volume descartavel)
  e roda, nesta ordem: dotnet build, dotnet test (os testes com banco usam o SQL Server desse ambiente), build e lint
  do front, teste simples da API/nginx e, se existir, o teste ponta a ponta (Playwright). No fim apaga o ambiente.
  Nao toca no ambiente de uso diario nem nos seus dados.
.PARAMETER SemDocker
  Nao sobe o Docker. Os testes com banco usam o SQL Server do .env (se SA_PASSWORD existir) ou sao dispensados.
.PARAMETER SemE2E
  Pula o teste ponta a ponta no navegador.
#>
param([switch]$SemDocker, [switch]$SemE2E)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\lib.ps1"
$inicio = Get-Date
$stackNoAr = $false
Push-Location $script:Raiz
try {
    Passo '1/6 dotnet build'
    Executar 'dotnet' @('build', 'backend/SisMaster.slnx', '--nologo', '-v', 'q')

    if ($SemDocker) {
        Importar-DotEnv
        if ($env:SA_PASSWORD) {
            $porta = if ($env:DB_PORT) { $env:DB_PORT } else { '1433' }
            $env:SISMASTER_TESTE_SQLSERVER = "Server=localhost,$porta;User Id=sa;Password=$($env:SA_PASSWORD);TrustServerCertificate=True"
            Write-Host 'Testes com banco: usando o SQL Server do .env (bancos temporarios, apagados ao fim).' -ForegroundColor Yellow
        } else {
            $env:SISMASTER_TESTE_SEM_BANCO = '1'
            Write-Host 'Testes com banco DISPENSADOS (sem SA_PASSWORD).' -ForegroundColor Yellow
        }
    } else {
        Passo '2/6 Docker do zero (ambiente isolado, banco vazio)'
        Subir-StackTeste
        $stackNoAr = $true
        $env:SISMASTER_TESTE_SQLSERVER = 'Server=localhost,1634;User Id=sa;Password=Teste#Verif2026x;TrustServerCertificate=True'
    }

    Passo '3/6 dotnet test'
    Executar 'dotnet' @('test', 'tests/SisMaster.WebApps.Tests', '--nologo')

    Passo '4/6 front: npm ci + build + lint'
    Push-Location 'frontend/admin'
    try {
        if (-not (Test-Path 'node_modules')) { Executar 'npm' @('ci') }
        Executar 'npm' @('run', 'build')
        Executar 'npm' @('run', 'lint')
    } finally { Pop-Location }

    if ($stackNoAr) {
        Passo '5/6 API e front no ar (banco vazio, migrations aplicadas)'
        $r = Invoke-WebRequest -Uri 'http://localhost:3300/api/associacoes' -UseBasicParsing
        if ($r.StatusCode -ne 200) { throw "GET /api/associacoes via nginx devolveu $($r.StatusCode)" }
        $p = Invoke-WebRequest -Uri 'http://localhost:3300/admin/' -UseBasicParsing
        if ($p.StatusCode -ne 200) { throw "/admin/ devolveu $($p.StatusCode)" }
        Write-Host 'OK.'

        if (-not $SemE2E -and (Test-Path (Join-Path $script:Raiz 'e2e/package.json'))) {
            Passo '6/6 teste ponta a ponta (Playwright)'
            $env:E2E_BASE_URL = 'http://localhost:3300'
            Push-Location 'e2e'
            try {
                if (-not (Test-Path 'node_modules')) { Executar 'npm' @('ci') }
                Executar 'npx' @('playwright', 'install', 'chromium')   # rapido quando ja esta instalado
                Executar 'npm' @('test')
            } finally { Pop-Location }
        }
    }
    $min = [math]::Round(((Get-Date) - $inicio).TotalMinutes, 1)
    Write-Host "`nTUDO VERDE em $min min." -ForegroundColor Green
} catch {
    Write-Host "`nFALHOU: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    if ($stackNoAr) { Parar-StackTeste }
    Pop-Location
}
