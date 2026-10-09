# Funcoes compartilhadas dos scripts. Uso: . "$PSScriptRoot\lib.ps1"
# (Somente ASCII neste arquivo: o Windows PowerShell 5.1 le arquivos sem BOM como ANSI.)

$script:Raiz = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Importar-DotEnv {
    # Le o .env da raiz e define as variaveis que ainda nao existem no ambiente.
    $arq = Join-Path $script:Raiz '.env'
    if (-not (Test-Path $arq)) { return }
    foreach ($linha in Get-Content $arq) {
        if ($linha -match '^\s*#' -or $linha -notmatch '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=(.*)$') { continue }
        $nome = $Matches[1]; $valor = $Matches[2].Trim()
        if (-not [Environment]::GetEnvironmentVariable($nome)) { [Environment]::SetEnvironmentVariable($nome, $valor) }
    }
}

function Passo($texto) { Write-Host ""; Write-Host "==> $texto" -ForegroundColor Cyan }

function Executar($comando, [string[]]$argumentos) {
    # Roda um comando externo e para o script se ele falhar.
    & $comando @argumentos
    if ($LASTEXITCODE -ne 0) {
        # Nunca imprime a senha: o valor que vem depois de -P e mascarado.
        $exibir = @(); $mascarar = $false
        foreach ($x in $argumentos) { if ($mascarar) { $exibir += '***'; $mascarar = $false } else { $exibir += $x; if ($x -ceq '-P') { $mascarar = $true } } }
        throw "Falhou: $comando $($exibir -join ' ') (codigo $LASTEXITCODE)"
    }
}

# ---- Ambiente de teste isolado (projeto Docker proprio, volume e portas proprios) ----
# Nunca toca no ambiente de uso diario (projeto "sismaster", volume sismaster_sqlserver_data).
function Subir-StackTeste([string]$projeto = 'sismaster-teste', [int]$portaFront = 3300, [int]$portaApi = 5400, [int]$portaDb = 1634) {
    $env:COMPOSE_PROJECT_NAME = $projeto
    $env:FRONT_PORT = "$portaFront"; $env:API_PORT = "$portaApi"; $env:DB_PORT = "$portaDb"
    $env:DB_NAME = 'SisMasterTeste'
    $env:SA_PASSWORD = 'Teste#Verif2026x'          # descartavel; o volume e apagado ao final
    Push-Location $script:Raiz
    try {
        Executar 'docker' @('compose', '--progress', 'quiet', 'down', '-v', '--remove-orphans')   # garante banco vazio
        Executar 'docker' @('compose', '--progress', 'quiet', 'up', '-d', '--build')
    } finally { Pop-Location }
    $url = "http://localhost:$portaApi/api/associacoes"
    for ($i = 0; $i -lt 60; $i++) {
        try { if ((Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5).StatusCode -eq 200) { return } } catch { }
        Start-Sleep -Seconds 3
    }
    throw "A API de teste nao respondeu em $url"
}

function Parar-StackTeste {
    Push-Location $script:Raiz
    try { & docker compose --progress quiet down -v --remove-orphans | Out-Null } finally { Pop-Location }
}
