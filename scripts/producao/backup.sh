#!/usr/bin/env bash
# Backup do banco de campeonatos e do de contas (SQL Server no Docker). Rode todo dia pelo cron, por exemplo às 03:00:
#   0 3 * * * cd /opt/sismaster && ./scripts/producao/backup.sh >> /var/log/sismaster-backup.log 2>&1
#
# O que faz: backup COPY_ONLY comprimido dos dois bancos, copia para PASTA_BACKUP (padrão /var/backups/sismaster), confere cada arquivo
# (RESTORE VERIFYONLY), apaga os com mais de DIAS_GUARDAR dias (padrão 14) e, se COPIA_EXTERNA estiver definida, copia para fora do servidor.
# COPIA_EXTERNA é um comando que recebe o arquivo como argumento, por exemplo: COPIA_EXTERNA="rclone copy" e DESTINO_EXTERNO="drive:sismaster".
# Um backup que só existe no mesmo servidor não protege de perda do servidor: configure a cópia externa antes de abrir ao público.
set -euo pipefail
cd "$(dirname "$0")/../.."
set -a; . "${ENV_FILE:-./.env}"; set +a

PROJETO="${COMPOSE_PROJECT_NAME:-sismaster}"
CONTAINER="${PROJETO}-sqlserver"
PASTA_BACKUP="${PASTA_BACKUP:-/var/backups/sismaster}"
DIAS_GUARDAR="${DIAS_GUARDAR:-14}"
BANCOS=("${DB_NAME:-SisMaster}" "${IDENTIDADE_DB_NAME:-SisMasterIdentidade}")
CARIMBO="$(date +%Y%m%d_%H%M)"
SQLCMD=(docker exec "$CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -b)

mkdir -p "$PASTA_BACKUP"
for banco in "${BANCOS[@]}"; do
  arquivo="${banco}_${CARIMBO}.bak"
  echo "[$(date +%F\ %T)] backup de $banco"
  "${SQLCMD[@]}" -Q "BACKUP DATABASE [$banco] TO DISK='/var/opt/mssql/backup-$arquivo' WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT"
  "${SQLCMD[@]}" -Q "RESTORE VERIFYONLY FROM DISK='/var/opt/mssql/backup-$arquivo' WITH CHECKSUM"
  docker cp "$CONTAINER:/var/opt/mssql/backup-$arquivo" "$PASTA_BACKUP/$arquivo"
  docker exec "$CONTAINER" rm -f "/var/opt/mssql/backup-$arquivo"
  chmod 600 "$PASTA_BACKUP/$arquivo"
  if [ -n "${COPIA_EXTERNA:-}" ]; then
    echo "[$(date +%F\ %T)] copiando $arquivo para fora do servidor"
    # shellcheck disable=SC2086
    $COPIA_EXTERNA "$PASTA_BACKUP/$arquivo" "${DESTINO_EXTERNO:?Defina DESTINO_EXTERNO no .env}"
  fi
done

find "$PASTA_BACKUP" -name '*.bak' -mtime +"$DIAS_GUARDAR" -print -delete
echo "[$(date +%F\ %T)] backup concluído em $PASTA_BACKUP"
