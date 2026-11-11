#!/usr/bin/env bash
# Cria ou atualiza os rótulos do backlog (docs/processo.md §7; spec do P1, P1-33). Idempotente: rodar de novo não duplica.
# Uso: bash scripts/rotulos.sh [owner/repo]   (padrão: o repositório do diretório atual)
set -euo pipefail
repo=(${1:+--repo "$1"})
while IFS='|' read -r nome cor descricao; do
    gh label create "$nome" --color "$cor" --description "$descricao" --force "${repo[@]}"
done <<'LISTA'
marco|5319E7|Trabalho que pede spec; entra no G1
bug|D73A4A|Comportamento errado; leva cenário de regressão
melhoria|0E8A16|Mudança pequena sem spec (um PR)
sugestao|FBCA04|Veio de #sugestoes ou de quem usa
producao|B60205|Veio de #alertas ou do Error Reporting
triagem|C5DEF5|Ainda não classificado
LISTA
