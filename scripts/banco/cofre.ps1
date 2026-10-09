<#
.SYNOPSIS
  Cofre de dados: guarda uma copia do banco do Docker numa instancia SQL Server da sua maquina e a traz de volta.
.DESCRIPTION
  Usa backup e restauracao (copia o banco inteiro). A origem NUNCA e alterada em "Guardar", e "Trazer" nunca
  sobrescreve nada sem -Substituir -Confirmo (e, nesse caso, faz antes um backup de seguranca do banco atual).

  Acoes:
    Guardar  Docker -> cofre. Cria no SQL local um banco novo SisMaster_cofre_AAAAMMDD_HHMM[_rotulo] (somente leitura).
    Listar   Mostra os bancos do cofre (SQL local) e os bancos do SQL do Docker.
    Trazer   Cofre -> Docker. Sem -Substituir cria um banco novo (SisMaster_restaurado_...) no Docker.
             Com -Substituir -Confirmo troca o banco SisMaster do Docker pelo do cofre.

  Configuracao (no .env, opcional): COFRE_SERVIDOR (padrao: nome desta maquina), COFRE_PASTA_TROCA
  (padrao C:\Users\Public\SisMasterCofre: pasta que o servico SQL local consegue ler e gravar).
  A instancia local usa autenticacao do Windows (Integrated Security).
.EXAMPLE
  .\scripts\banco\cofre.ps1 Guardar -Rotulo etapa-0
  .\scripts\banco\cofre.ps1 Listar
  .\scripts\banco\cofre.ps1 Trazer -Nome SisMaster_cofre_20261009_0012_etapa-0
  .\scripts\banco\cofre.ps1 Trazer -Nome SisMaster_cofre_20261009_0012_etapa-0 -Substituir -Confirmo
#>
param(
    [Parameter(Mandatory = $true, Position = 0)][ValidateSet('Guardar', 'Listar', 'Trazer')][string]$Acao,
    [string]$Rotulo,
    [string]$Nome,
    [switch]$Substituir,
    [switch]$Confirmo,
    [string]$Container = 'sismaster-sqlserver',
    [string]$Banco = 'SisMaster',
    [string]$Servidor,
    [string]$PastaTroca
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\..\lib.ps1"
Importar-DotEnv
if (-not $env:SA_PASSWORD) { throw 'Defina SA_PASSWORD no arquivo .env.' }
if (-not $Servidor) { $Servidor = if ($env:COFRE_SERVIDOR) { $env:COFRE_SERVIDOR } else { $env:COMPUTERNAME } }
if (-not $PastaTroca) { $PastaTroca = if ($env:COFRE_PASTA_TROCA) { $env:COFRE_PASTA_TROCA } else { 'C:\Users\Public\SisMasterCofre' } }
$pastaBackups = Join-Path $script:Raiz 'backups'
$sqlcmdDocker = '/opt/mssql-tools18/bin/sqlcmd'
New-Item -ItemType Directory -Force $PastaTroca | Out-Null
New-Item -ItemType Directory -Force $pastaBackups | Out-Null

# ---------------- acesso aos dois SQL Servers ----------------
function Sql-Local([string]$consulta, [string]$separador = " ") {
    # Windows Authentication; Encrypt + TrustServerCertificate (-C), como na connection string informada.
    $saida = & sqlcmd -S $Servidor -E -C -h -1 -W -b -s $separador -Q $consulta
    if ($LASTEXITCODE -ne 0) { throw "SQL local falhou: $($saida -join ' ')" }
    $saida
}
function Sql-Docker([string]$consulta, [string]$separador = " ") {
    $saida = & docker exec $Container $sqlcmdDocker -S localhost -U sa -P $env:SA_PASSWORD -C -h -1 -W -b -s $separador -Q $consulta
    if ($LASTEXITCODE -ne 0) { throw "SQL do Docker falhou: $(($saida -join ' ') -replace [regex]::Escape($env:SA_PASSWORD), '***')" }
    $saida
}
function Apagar-Arquivo([string]$caminho) { if (Test-Path -LiteralPath $caminho) { [System.IO.File]::Delete($caminho) } }

function Nomes-Logicos([scriptblock]$sql, [string]$arquivo) {
    # Le (nome logico, tipo) dos arquivos do backup: tipo D = dados, L = log. Colunas separadas por "|".
    $linhas = & $sql "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'$arquivo'" "|"
    $r = @()
    foreach ($l in $linhas) {
        $c = $l.Split('|')
        if ($c.Count -ge 3 -and ($c[2].Trim() -eq 'D' -or $c[2].Trim() -eq 'L')) {
            $r += [pscustomobject]@{ Logico = $c[0].Trim(); Tipo = $c[2].Trim() }
        }
    }
    if ($r.Count -eq 0) { throw "Nao consegui ler os arquivos do backup $arquivo" }
    $r
}

function Contagens([scriptblock]$sql, [string]$banco) {
    $q = "SET NOCOUNT ON; SELECT 'Pessoas=' + CAST((SELECT COUNT(*) FROM [$banco].dbo.Pessoas) AS varchar(12)) + ' Inscricoes=' + CAST((SELECT COUNT(*) FROM [$banco].dbo.Inscricoes) AS varchar(12)) + ' Equipes=' + CAST((SELECT COUNT(*) FROM [$banco].dbo.Equipes) AS varchar(12)) + ' Jogos=' + CAST((SELECT COUNT(*) FROM [$banco].dbo.Jogos) AS varchar(12)) + ' Sumulas=' + CAST((SELECT COUNT(*) FROM [$banco].dbo.Sumulas) AS varchar(12)) + ' Eventos=' + CAST((SELECT COUNT(*) FROM [$banco].dbo.EventosSumula) AS varchar(12))"
    (& $sql $q | Select-Object -First 1).Trim()
}

function Backup-Docker([string]$banco, [string]$nomeArquivo) {
    Sql-Docker "BACKUP DATABASE [$banco] TO DISK = N'/tmp/$nomeArquivo' WITH INIT, COPY_ONLY, COMPRESSION, CHECKSUM" | Out-Null
}

$stamp = Get-Date -Format 'yyyyMMdd_HHmm'
$sqlLocal = { param($q, $sep = ' ') Sql-Local $q $sep }
$sqlDocker = { param($q, $sep = ' ') Sql-Docker $q $sep }

switch ($Acao) {

    'Listar' {
        # Colunas separadas (sem concatenar texto): as instancias tem collations diferentes.
        function Mostrar($linhas) {
            $achou = $false
            foreach ($l in $linhas) {
                $c = $l.Split('|')
                if ($c.Count -ge 4) { $achou = $true; Write-Host ("  {0,-55} criado em {1}  {2}{3}" -f $c[0].Trim(), $c[1].Trim(), $c[2].Trim(), $(if ($c[3].Trim() -eq '1') { ' (somente leitura)' } else { '' })) }
            }
            if (-not $achou) { Write-Host '  (nenhum)' }
        }
        Write-Host "== Cofre (SQL local: $Servidor) ==" -ForegroundColor Cyan
        Mostrar (Sql-Local "SET NOCOUNT ON; SELECT name, CONVERT(varchar(16), create_date, 120), state_desc, CAST(is_read_only AS int) FROM sys.databases WHERE name LIKE N'SisMaster%' ORDER BY create_date DESC" '|')
        Write-Host "== SQL do Docker ($Container) ==" -ForegroundColor Cyan
        Mostrar (Sql-Docker "SET NOCOUNT ON; SELECT name, CONVERT(varchar(16), create_date, 120), state_desc, CAST(is_read_only AS int) FROM sys.databases WHERE database_id > 4 ORDER BY create_date DESC" '|')
    }

    'Guardar' {
        $sufixo = ''
        if ($Rotulo) { $sufixo = '_' + (($Rotulo -replace '[^A-Za-z0-9\-]', '-').Trim('-')) }
        $destino = "${Banco}_cofre_$stamp$sufixo"
        $arq = "$destino.bak"

        Passo "Backup de $Banco no Docker (a origem nao e alterada)"
        Backup-Docker $Banco $arq
        Executar 'docker' @('cp', "${Container}:/tmp/$arq", (Join-Path $PastaTroca $arq))
        Executar 'docker' @('exec', '-u', '0', $Container, 'rm', '-f', "/tmp/$arq")
        Copy-Item (Join-Path $PastaTroca $arq) (Join-Path $pastaBackups $arq) -Force

        Passo "Restaurando no SQL local ($Servidor) como $destino"
        $caminhoLocal = Join-Path $PastaTroca $arq
        try {
        $dados = (Sql-Local "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(260))" | Select-Object -First 1).Trim()
        $log = (Sql-Local "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(260))" | Select-Object -First 1).Trim()
        $arqs = Nomes-Logicos $sqlLocal $caminhoLocal
        $mover = @(); $i = 0
        foreach ($a in $arqs) {
            $i++
            $fisico = if ($a.Tipo -eq 'D') { Join-Path $dados "$destino$(if ($i -gt 1) { "_$i" }).mdf" } else { Join-Path $log "${destino}_log$(if ($i -gt 2) { "_$i" }).ldf" }
            $mover += "MOVE N'$($a.Logico)' TO N'$fisico'"
        }
        Sql-Local "RESTORE DATABASE [$destino] FROM DISK = N'$caminhoLocal' WITH $($mover -join ', '), CHECKSUM, RECOVERY" | Out-Null
        Sql-Local "ALTER DATABASE [$destino] SET READ_ONLY WITH ROLLBACK IMMEDIATE" | Out-Null
        } finally { Apagar-Arquivo $caminhoLocal }   # a pasta de troca e legivel por todos os usuarios desta maquina: nada fica nela

        Passo 'Conferencia (origem x cofre)'
        $a = Contagens $sqlDocker $Banco
        $b = Contagens $sqlLocal $destino
        Write-Host "Docker : $a"
        Write-Host "Cofre  : $b"
        if ($a -ne $b) { throw 'As contagens do cofre diferem da origem!' }
        Write-Host "`nGuardado no cofre: $destino (somente leitura)." -ForegroundColor Green
        Write-Host "Copia do arquivo de backup: $(Join-Path $pastaBackups $arq)"
    }

    'Trazer' {
        if (-not $Nome) { throw 'Informe -Nome <banco do cofre>. Veja os nomes com: .\scripts\banco\cofre.ps1 Listar' }
        $existe = (Sql-Local "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = N'$Nome'" | Select-Object -First 1).Trim()
        if ($existe -ne '1') { throw "O cofre nao tem um banco chamado '$Nome'." }
        if ($Substituir -and -not $Confirmo) {
            throw "Isto SUBSTITUI o banco '$Banco' do Docker pelo conteudo de '$Nome'. Para confirmar, rode de novo com -Substituir -Confirmo (um backup de seguranca do banco atual e feito antes)."
        }

        $arq = "$Nome.bak"
        Passo "Backup do cofre ($Nome) para a pasta de troca"
        try {
            Sql-Local "BACKUP DATABASE [$Nome] TO DISK = N'$(Join-Path $PastaTroca $arq)' WITH INIT, COPY_ONLY, COMPRESSION, CHECKSUM" | Out-Null
            Executar 'docker' @('cp', (Join-Path $PastaTroca $arq), "${Container}:/tmp/$arq")
        } finally { Apagar-Arquivo (Join-Path $PastaTroca $arq) }   # nada fica na pasta de troca (legivel por todos os usuarios desta maquina)

        $alvo = if ($Substituir) { $Banco } else { "${Banco}_restaurado_$stamp" }
        $arqs = Nomes-Logicos $sqlDocker "/tmp/$arq"
        $mover = @(); $i = 0
        foreach ($a in $arqs) {
            $i++
            $fisico = if ($a.Tipo -eq 'D') { "/var/opt/mssql/data/$alvo$(if ($i -gt 1) { "_$i" }).mdf" } else { "/var/opt/mssql/data/${alvo}_log$(if ($i -gt 2) { "_$i" }).ldf" }
            $mover += "MOVE N'$($a.Logico)' TO N'$fisico'"
        }

        if ($Substituir) {
            Passo "Backup de seguranca de $Banco (Docker) antes de substituir"
            $seg = "$Banco-antes-de-trazer-$stamp.bak"
            Backup-Docker $Banco $seg
            Executar 'docker' @('cp', "${Container}:/tmp/$seg", (Join-Path $pastaBackups $seg))
            Executar 'docker' @('exec', '-u', '0', $Container, 'rm', '-f', "/tmp/$seg")
            Write-Host "Seguranca: $(Join-Path $pastaBackups $seg)"

            Passo "Substituindo $Banco no Docker pelo conteudo de $Nome"
            try {
                Sql-Docker "ALTER DATABASE [$Banco] SET SINGLE_USER WITH ROLLBACK IMMEDIATE" | Out-Null
                Sql-Docker "RESTORE DATABASE [$Banco] FROM DISK = N'/tmp/$arq' WITH $($mover -join ', '), REPLACE, CHECKSUM, RECOVERY" | Out-Null
            } finally {
                Sql-Docker "ALTER DATABASE [$Banco] SET MULTI_USER" | Out-Null
            }
        } else {
            Passo "Restaurando no Docker como NOVO banco: $alvo (o banco $Banco nao e tocado)"
            Sql-Docker "RESTORE DATABASE [$alvo] FROM DISK = N'/tmp/$arq' WITH $($mover -join ', '), CHECKSUM, RECOVERY" | Out-Null
        }
        Sql-Docker "ALTER DATABASE [$alvo] SET READ_WRITE WITH ROLLBACK IMMEDIATE" | Out-Null   # o cofre e somente leitura; o destino nao
        Executar 'docker' @('exec', '-u', '0', $Container, 'rm', '-f', "/tmp/$arq")

        Passo 'Conferencia (cofre x Docker)'
        $a = Contagens $sqlLocal $Nome
        $b = Contagens $sqlDocker $alvo
        Write-Host "Cofre  : $a"
        Write-Host "Docker : $b"
        if ($a -ne $b) { throw 'As contagens do Docker diferem do cofre!' }
        if ($Substituir) { Write-Host "`nO banco $Banco do Docker agora tem os dados do cofre ($Nome)." -ForegroundColor Green }
        else { Write-Host "`nCriado no Docker o banco $alvo (para usa-lo no lugar de $Banco, rode de novo com -Substituir -Confirmo)." -ForegroundColor Green }
    }
}
