<#
.SYNOPSIS
  Aplica o seed da ABABAS num banco. SO RODA QUANDO VOCE MANDAR: nada executa isto automaticamente.
.DESCRIPTION
  Insere (sem apagar nada, e sem duplicar o que ja existe) a associacao, as categorias e os locais
  (01-ababas-base.sql) e, com -ComPessoas, as pessoas (dados-locais/02-pessoas-ababas.sql).
  O banco precisa ter as migrations aplicadas (a API aplica ao subir). O seed e idempotente.
.PARAMETER Container
  Container do SQL Server alvo. Padrao: sismaster-sqlserver (uso diario).
.PARAMETER Banco
  Nome do banco. Padrao: SisMaster.
.PARAMETER Senha
  Senha do SA do alvo. Padrao: SA_PASSWORD do .env.
.PARAMETER ComPessoas
  Inclui as pessoas (dados pessoais, vindos de scripts/seed/dados-locais).
.EXAMPLE
  .\scripts\seed\aplicar-seed.ps1 -ComPessoas
#>
param([string]$Container = 'sismaster-sqlserver', [string]$Banco = 'SisMaster', [string]$Senha, [switch]$ComPessoas)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\lib.ps1"
Importar-DotEnv
if (-not $Senha) { $Senha = $env:SA_PASSWORD }
if (-not $Senha) { throw 'Informe -Senha ou defina SA_PASSWORD no .env.' }
$sqlcmd = '/opt/mssql-tools18/bin/sqlcmd'

$arquivos = @(Join-Path $PSScriptRoot '01-ababas-base.sql')
if ($ComPessoas) {
    $p = Join-Path $PSScriptRoot 'dados-locais/02-pessoas-ababas.sql'
    if (-not (Test-Path $p)) { throw "Nao achei $p. Gere com scripts/seed/exportar-seed.ps1 (a partir de um banco que tenha as pessoas)." }
    $arquivos += $p
}
foreach ($a in $arquivos) { if (-not (Test-Path $a)) { throw "Arquivo de seed nao encontrado: $a" } }

Write-Host "Aplicando seed em $Container / $Banco ..." -ForegroundColor Cyan
foreach ($a in $arquivos) {
    $nome = [IO.Path]::GetFileName($a)
    Executar 'docker' @('cp', $a, "${Container}:/tmp/$nome")
    Executar 'docker' @('exec', $Container, $sqlcmd, '-S', 'localhost', '-U', 'sa', '-P', $Senha, '-C', '-d', $Banco, '-f', '65001', '-b', '-i', "/tmp/$nome")
    Write-Host "  ok: $nome"
}
Write-Host 'Seed aplicado.' -ForegroundColor Green
