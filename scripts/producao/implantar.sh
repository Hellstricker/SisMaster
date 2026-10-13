#!/usr/bin/env bash
# Atualiza o sistema no servidor: backup, versão nova, subida, conferência e, se algo falhar, instruções para voltar atrás.
# Uso (no servidor, na pasta do projeto):  ./scripts/producao/implantar.sh [tag-ou-commit]     (sem argumento, usa o que está no ramo atual)
# Voltar atrás:                            ./scripts/producao/implantar.sh <tag-ou-commit-anterior>
# As migrations do banco são aplicadas sozinhas quando a API sobe. Por isso o backup vem ANTES de tudo: ele é a volta atrás do banco.
set -euo pipefail
cd "$(dirname "$0")/../.."
set -a; . "${ENV_FILE:-./.env}"; set +a
COMPOSE=(docker compose -f docker-compose.yml -f docker-compose.prod.yml)
SIGLA="${SITE_ASSOCIACAO_SIGLA:-ABABAS}"
ANTES="$(git rev-parse --short HEAD)"

echo "Versão atual: $ANTES"
echo "1/4 backup antes de atualizar"
./scripts/producao/backup.sh

echo "2/4 buscando a versão nova"
if [ -n "${1:-}" ]; then git fetch --tags --quiet; git checkout --quiet "$1"; else git pull --ff-only; fi
echo "Versão nova: $(git rev-parse --short HEAD)"

echo "3/4 construindo e subindo"
"${COMPOSE[@]}" up -d --build

echo "4/4 conferindo (até 2 minutos)"
for _ in $(seq 1 24); do
  if curl -fsS "https://${DOMINIO}/api/publico/associacoes/${SIGLA}" >/dev/null 2>&1; then
    echo "OK: o site respondeu em https://${DOMINIO}"
    exit 0
  fi
  sleep 5
done

echo "FALHOU: o site não respondeu. Para voltar à versão anterior:"
echo "  ./scripts/producao/implantar.sh $ANTES"
echo "Se a versão nova já aplicou uma migration, restaure também o banco do backup feito no passo 1 (docs/producao.md, seção Restaurar)."
exit 1
