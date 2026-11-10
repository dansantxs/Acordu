#!/usr/bin/env bash
# Aviso de operação no Discord (ADR 0012, F13g): sucesso em #deploy, falha em #alertas.
# Uso: avisar.sh <status do job> <texto do sucesso> <texto da falha>, com DISCORD_DEPLOY e DISCORD_ALERTAS no ambiente.
# Sem o webhook do canal, pula; se o Discord falhar, só avisa no log (o aviso nunca derruba o workflow). A URL do
# webhook é segredo: não aparece em nenhuma saída.
set -uo pipefail

status="${1:?status do job}"
ok="${2:?texto do sucesso}"
falha="${3:?texto da falha}"

if [[ "$status" == "success" ]]; then
  canal="#deploy"; webhook="${DISCORD_DEPLOY:-}"; texto="$ok"
else
  canal="#alertas"; webhook="${DISCORD_ALERTAS:-}"; texto="$falha"
fi

if [[ -z "$webhook" ]]; then
  echo "Sem webhook de $canal; aviso pulado."
  exit 0
fi

# Menções desligadas: nenhum texto aciona @everyone, @here ou um cargo.
corpo=$(jq -n --arg t "$texto" '{content: $t, allowed_mentions: {parse: []}}')
# O stderr do curl fica de fora (pode trazer a URL); o código de saída diz o motivo (22: HTTP de erro, 28: tempo).
curl -fsS --max-time 10 -o /dev/null -H 'Content-Type: application/json' -d "$corpo" "$webhook" 2>/dev/null
codigo=$?
if [[ $codigo -eq 0 ]]; then
  echo "Aviso em $canal."
else
  echo "::warning::O aviso em $canal não saiu (curl $codigo; o workflow segue)."
fi
exit 0
