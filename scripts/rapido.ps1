<#
.SYNOPSIS
  Ciclo rapido de teste: um ambiente ISOLADO que fica no ar entre as rodadas, reconstroi so o que mudou e roda so o teste pedido.
.DESCRIPTION
  Usa o projeto Docker "sismaster-rapido" (portas 3400/5500/1734, banco e volume proprios, descartaveis). Nao toca no ambiente
  de uso diario (projeto "sismaster") nem no do verificar.ps1 (projeto "sismaster-teste"). Antes de CADA commit, rode o
  scripts\verificar.ps1 inteiro: este script e para iterar, nao para aprovar.

  Exemplos:
    .\scripts\rapido.ps1 -Reconstruir frontend -Teste associacoes      # mudou so a tela: ~1 min
    .\scripts\rapido.ps1 -Reconstruir api -Dotnet Publico              # mudou a API: reconstroi a API e roda testes .NET filtrados
    .\scripts\rapido.ps1 -Teste "fluxo-completo -g desativar"          # so um teste, sem reconstruir nada
    .\scripts\rapido.ps1 -Zerar                                        # recria tudo com banco vazio
    .\scripts\rapido.ps1 -Parar                                        # derruba e apaga o ambiente rapido
.PARAMETER Reconstruir
  O que reconstruir: frontend, api, identidade, site (um ou mais, separados por virgula). Vazio = nada (reaproveita o que esta no ar).
.PARAMETER Teste
  Argumentos do Playwright, ex.: "jogos-encerrar-a6" ou "fluxo-completo -g desativar".
  O teste "primeiro acesso" do fluxo-completo so passa com banco vazio (use -Zerar).
.PARAMETER Dotnet
  Filtro (FullyQualifiedName~) dos testes .NET, que usam o SQL Server deste ambiente.
#>
param(
    [string[]]$Reconstruir = @(),
    [string]$Teste = '',
    [string]$Dotnet = '',
    [switch]$Zerar,
    [switch]$Parar,
    [switch]$Entrar
)

$ErrorActionPreference = 'Stop'
Remove-Item Env:FORCE_COLOR, Env:NO_COLOR -ErrorAction SilentlyContinue
. "$PSScriptRoot\lib.ps1"

$projeto = 'sismaster-rapido'; $portaFront = 3400; $portaApi = 5500; $portaDb = 1734
$env:COMPOSE_PROJECT_NAME = $projeto
$env:FRONT_PORT = "$portaFront"; $env:API_PORT = "$portaApi"; $env:DB_PORT = "$portaDb"
$env:DB_NAME = 'SisMasterRapido'
$env:SA_PASSWORD = 'Teste#Verif2026x'
$env:JWT_SEGREDO = 'segredo-de-teste-descartavel-0123456789abcdef'
$env:SITE_ASSOCIACAO_SIGLA = 'SITEE2E'
$env:EMAIL_PROVEDOR = 'Memoria'
$env:LIMITE_CADASTROS_POR_HORA = '1000'
$env:LIMITE_LOGINS_POR_MINUTO = '500'
$env:ADMIN_EMAIL = 'admin@teste.local'; $env:ADMIN_NOME = 'Admin Teste'; $env:ADMIN_SENHA_INICIAL = 'Teste#Admin2026'

$servico = @{ frontend = 'frontend'; api = 'campeonato-api'; identidade = 'identidade-api'; site = 'site' }

function Compose([string[]]$argumentos) { Executar 'docker' (@('compose', '--progress', 'quiet') + $argumentos) }

function Aguardar-Pronto {
    $login = "http://localhost:$portaFront/api/identidade/autenticar"
    $corpo = @{ email = $env:ADMIN_EMAIL; password = $env:ADMIN_SENHA_INICIAL } | ConvertTo-Json
    $corpoNovo = @{ email = $env:ADMIN_EMAIL; password = 'Admin#E2E2026x' } | ConvertTo-Json   # depois do teste de primeiro acesso
    for ($i = 0; $i -lt 80; $i++) {
        foreach ($c in @($corpo, $corpoNovo)) {
            try {
                $r = Invoke-RestMethod -Uri $login -Method Post -Body $c -ContentType application/json -TimeoutSec 5
                $h = @{ Authorization = "Bearer $($r.accessToken)" }
                if ((Invoke-WebRequest -Uri "http://localhost:$portaApi/api/associacoes" -Headers $h -UseBasicParsing -TimeoutSec 5).StatusCode -eq 200) { return }
            } catch { }
        }
        Start-Sleep -Seconds 2
    }
    throw "O ambiente rapido nao ficou pronto (porta $portaFront)"
}

Push-Location $script:Raiz
try {
    $inicio = Get-Date
    if ($Parar) {
        Passo 'Derrubando o ambiente rapido (apaga o banco dele)'
        & docker compose --progress quiet down -v --remove-orphans | Out-Null
        Write-Host 'Pronto.'; return
    }

    $noAr = @(& docker compose ps -q 2>$null).Count -gt 0
    if ($Zerar -or -not $noAr) {
        Passo 'Subindo o ambiente rapido do zero (primeira vez leva alguns minutos)'
        Compose @('down', '-v', '--remove-orphans')
        Compose @('up', '-d', '--build')
        Aguardar-Pronto
    } elseif ($Reconstruir.Count -gt 0) {
        $alvos = @()
        foreach ($nome in ($Reconstruir | ForEach-Object { $_ -split ',' })) {
            $n = $nome.Trim().ToLower()
            if (-not $servico.ContainsKey($n)) { throw "Servico desconhecido: $n (use: $($servico.Keys -join ', '))" }
            $alvos += $servico[$n]
        }
        Passo "Reconstruindo: $($alvos -join ', ')"
        Compose (@('up', '-d', '--build') + $alvos)
        Aguardar-Pronto
    } else {
        Aguardar-Pronto
    }
    Write-Host "Ambiente rapido no ar: http://localhost:$portaFront/  (admin: $($env:ADMIN_EMAIL))" -ForegroundColor Green

    if ($Dotnet) {
        Passo "dotnet test (filtro: $Dotnet)"
        $env:SISMASTER_TESTE_SQLSERVER = "Server=localhost,$portaDb;User Id=sa;Password=$($env:SA_PASSWORD);TrustServerCertificate=True"
        Executar 'dotnet' @('test', 'tests/SisMaster.WebApps.Tests', '--nologo', '--filter', "FullyQualifiedName~$Dotnet")
    }

    if ($Teste) {
        Passo "Playwright: $Teste"
        $env:E2E_BASE_URL = "http://localhost:$portaFront"
        $env:E2E_ADMIN_EMAIL = $env:ADMIN_EMAIL; $env:E2E_ADMIN_SENHA_INICIAL = $env:ADMIN_SENHA_INICIAL
        Push-Location 'e2e'
        try {
            if (-not (Test-Path 'node_modules')) { Executar 'npm' @('ci') }
            Executar 'npx' (@('playwright', 'test') + ($Teste -split '\s+'))
        } finally { Pop-Location }
    }

    $seg = [math]::Round(((Get-Date) - $inicio).TotalSeconds)
    Write-Host "`nOK em $seg s." -ForegroundColor Green
} catch {
    Write-Host "`nFALHOU: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}
