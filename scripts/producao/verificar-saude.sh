#!/usr/bin/env bash
# Confere se o sistema está de pé. Use no cron a cada 5 minutos (ou aponte um serviço de monitoramento de disponibilidade para a mesma URL).
# Sai com erro e escreve no log quando algo não responde; com ALERTA_URL definida (um webhook de chat, por exemplo), também avisa por lá.
#   */5 * * * * cd /opt/sismaster && ./scripts/producao/verificar-saude.sh >> /var/log/sismaster-saude.log 2>&1
set -uo pipefail
cd "$(dirname "$0")/../.."
set -a; . "${ENV_FILE:-./.env}"; set +a
SIGLA="${SITE_ASSOCIACAO_SIGLA:-ABABAS}"
falhas=""

conferir() { curl -fsS --max-time 10 "$2" >/dev/null 2>&1 || falhas="$falhas $1"; }
conferir "site" "https://${DOMINIO}/"
conferir "api-publica" "https://${DOMINIO}/api/publico/associacoes/${SIGLA}"
# A Identidade (login) responde 400/401 a um pedido vazio; erro de servidor ou silêncio é que indicam problema.
codigo=$(curl -s --max-time 10 -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' -d '{}' "https://${DOMINIO}/api/identidade/autenticar")
case "$codigo" in 400|401|429) ;; *) falhas="$falhas identidade($codigo)" ;; esac

if [ -n "$falhas" ]; then
  mensagem="[$(date +%F\ %T)] SisMaster FORA DO AR ou lento:$falhas (${DOMINIO})"
  echo "$mensagem"
  [ -n "${ALERTA_URL:-}" ] && curl -fsS --max-time 10 -H 'Content-Type: application/json' -d "{\"text\":\"$mensagem\"}" "$ALERTA_URL" >/dev/null || true
  exit 1
fi
echo "[$(date +%F\ %T)] ok"
