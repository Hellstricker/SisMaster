#!/usr/bin/env bash
# Prova que o backup serve: restaura o backup mais recente num banco TEMPORÁRIO (nunca toca nos bancos de verdade), confere se as
# tabelas principais têm dados, cronometra e apaga o temporário. Rode depois de configurar o backup e, depois, uma vez por mês.
# Uso: ./scripts/producao/testar-restauracao.sh [arquivo.bak]   (sem argumento, usa o mais recente do banco principal)
set -euo pipefail
cd "$(dirname "$0")/../.."
set -a; . "${ENV_FILE:-./.env}"; set +a

PROJETO="${COMPOSE_PROJECT_NAME:-sismaster}"
CONTAINER="${PROJETO}-sqlserver"
PASTA_BACKUP="${PASTA_BACKUP:-/var/backups/sismaster}"
BANCO="${DB_NAME:-SisMaster}"
TEMP="${BANCO}_teste_restauracao"
ARQUIVO="${1:-$(ls -1t "$PASTA_BACKUP/${BANCO}_"*.bak 2>/dev/null | head -1)}"
[ -n "$ARQUIVO" ] && [ -f "$ARQUIVO" ] || { echo "Nenhum backup encontrado em $PASTA_BACKUP"; exit 1; }
SQLCMD=(docker exec "$CONTAINER" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -C -b)

echo "Restaurando $(basename "$ARQUIVO") em $TEMP ..."
inicio=$(date +%s)
docker cp "$ARQUIVO" "$CONTAINER:/var/opt/mssql/teste-restauracao.bak"
trap '"${SQLCMD[@]}" -Q "IF DB_ID(N'"'"'$TEMP'"'"') IS NOT NULL BEGIN ALTER DATABASE [$TEMP] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$TEMP]; END" >/dev/null 2>&1 || true; docker exec "$CONTAINER" rm -f /var/opt/mssql/teste-restauracao.bak' EXIT

# Nomes lógicos dos arquivos: lidos do próprio backup.
mapfile -t nomes < <("${SQLCMD[@]}" -h -1 -W -s '|' -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK='/var/opt/mssql/teste-restauracao.bak'" | awk -F'|' 'NF>2 {print $1"|"$3}')
mover=""
for n in "${nomes[@]}"; do
  logico="${n%%|*}"; tipo="${n##*|}"
  if [ "$tipo" = "L" ]; then destino="/var/opt/mssql/data/${TEMP}_log.ldf"; else destino="/var/opt/mssql/data/${TEMP}_${logico}.mdf"; fi
  mover="$mover, MOVE N'$logico' TO N'$destino'"
done
"${SQLCMD[@]}" -Q "RESTORE DATABASE [$TEMP] FROM DISK='/var/opt/mssql/teste-restauracao.bak' WITH REPLACE $mover"

echo "Conferindo os dados restaurados:"
"${SQLCMD[@]}" -d "$TEMP" -Q "
SELECT 'Associacoes' AS tabela, COUNT(*) AS linhas FROM Associacoes UNION ALL
SELECT 'Pessoas', COUNT(*) FROM Pessoas UNION ALL
SELECT 'Inscricoes', COUNT(*) FROM Inscricoes UNION ALL
SELECT 'Equipes', COUNT(*) FROM Equipes UNION ALL
SELECT 'Jogos', COUNT(*) FROM Jogos UNION ALL
SELECT 'Sumulas', COUNT(*) FROM Sumulas;"
fim=$(date +%s)
echo "Restauração conferida em $((fim - inicio)) segundos (banco temporário apagado)."
