<#
.SYNOPSIS
  Exporta os dados atuais do banco para os arquivos de seed (so le o banco; nao altera nada nele).
.DESCRIPTION
  Gera:
    scripts/seed/01-ababas-base.sql                  associacao, categorias e locais (sem dados pessoais; vai para o Git)
    scripts/seed/dados-locais/02-pessoas-ababas.sql  pessoas (DADOS PESSOAIS; ignorado pelo Git)
  Opcionalmente (-Backup) gera tambem um backup completo (.bak) em backups/ (ignorado pelo Git).
.PARAMETER Container
  Container do SQL Server. Padrao: sismaster-sqlserver (ambiente de uso diario).
.PARAMETER Banco
  Nome do banco. Padrao: SisMaster.
#>
param([string]$Container = 'sismaster-sqlserver', [string]$Banco = 'SisMaster', [switch]$Backup)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\lib.ps1"
Importar-DotEnv
if (-not $env:SA_PASSWORD) { throw 'Defina SA_PASSWORD no arquivo .env.' }
$sqlcmd = '/opt/mssql-tools18/bin/sqlcmd'
$pasta = $PSScriptRoot

function Exportar([string]$scriptSql, [string]$destino) {
    $nome = [IO.Path]::GetFileName($scriptSql)
    Executar 'docker' @('cp', $scriptSql, "${Container}:/tmp/$nome")
    Executar 'docker' @('exec', $Container, $sqlcmd, '-S', 'localhost', '-U', 'sa', '-P', $env:SA_PASSWORD, '-C', '-d', $Banco,
        '-h', '-1', '-W', '-w', '8000', '-f', '65001', '-b', '-i', "/tmp/$nome", '-o', "/tmp/saida-$nome.txt")
    New-Item -ItemType Directory -Force (Split-Path $destino) | Out-Null
    Executar 'docker' @('cp', "${Container}:/tmp/saida-$nome.txt", $destino)
    Write-Host "Gerado: $destino ($((Get-Content $destino | Measure-Object -Line).Lines) linhas)"
}

Exportar (Join-Path $pasta 'sql/exportar-base.sql')    (Join-Path $pasta '01-ababas-base.sql')
Exportar (Join-Path $pasta 'sql/exportar-pessoas.sql') (Join-Path $pasta 'dados-locais/02-pessoas-ababas.sql')

if ($Backup) {
    $arq = "$Banco-$(Get-Date -Format 'yyyyMMdd-HHmm').bak"
    Executar 'docker' @('exec', $Container, $sqlcmd, '-S', 'localhost', '-U', 'sa', '-P', $env:SA_PASSWORD, '-C', '-b', '-Q',
        "BACKUP DATABASE [$Banco] TO DISK = N'/tmp/$arq' WITH INIT, COMPRESSION, CHECKSUM")
    $dest = Join-Path $script:Raiz "backups/$arq"
    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
    Executar 'docker' @('cp', "${Container}:/tmp/$arq", $dest)
    Write-Host "Backup completo: $dest ($([math]::Round((Get-Item $dest).Length / 1MB, 1)) MB)"
}
