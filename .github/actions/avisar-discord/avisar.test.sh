#!/usr/bin/env bash
# Testes do avisar.sh (F13-31 a F13-33) com um curl falso: canal pelo status, menções desligadas, pular sem webhook e
# nunca falhar o workflow. Roda na CI (job "Ações") e à mão: bash .github/actions/avisar-discord/avisar.test.sh
set -euo pipefail
aqui="$(cd "$(dirname "$0")" && pwd)"
falso="$(mktemp -d)"
trap 'rm -rf "$falso"' EXIT
cat > "$falso/curl" <<'SH'
#!/usr/bin/env bash
printf '%s\n' "$@" > "$FALSO_ULTIMA"; exit "${FALSO_SAIDA:-0}"
SH
chmod +x "$falso/curl"
export PATH="$falso:$PATH" FALSO_ULTIMA="$falso/ultima"
falhas=0
confere() { if eval "$2"; then echo "ok   $1"; else echo "FALHOU $1"; falhas=$((falhas + 1)); fi; }

rm -f "$FALSO_ULTIMA"
saida=$(DISCORD_DEPLOY=https://d/deploy DISCORD_ALERTAS=https://d/alertas "$aqui/avisar.sh" success "ok @everyone" "falhou")
confere "sucesso vai para #deploy" 'grep -qx "https://d/deploy" "$FALSO_ULTIMA"'
confere "menções desligadas" 'grep -q "\"parse\": \[\]" "$FALSO_ULTIMA"'
confere "texto do sucesso" 'grep -q "ok @everyone" "$FALSO_ULTIMA"'

DISCORD_DEPLOY=https://d/deploy DISCORD_ALERTAS=https://d/alertas "$aqui/avisar.sh" failure "ok" "falhou x" > /dev/null
confere "falha vai para #alertas" 'grep -qx "https://d/alertas" "$FALSO_ULTIMA" && grep -q "falhou x" "$FALSO_ULTIMA"'

DISCORD_DEPLOY=https://d/deploy DISCORD_ALERTAS=https://d/alertas "$aqui/avisar.sh" cancelled "ok" "cancelado" > /dev/null
confere "cancelado também é #alertas" 'grep -qx "https://d/alertas" "$FALSO_ULTIMA"'

rm -f "$FALSO_ULTIMA"
saida=$("$aqui/avisar.sh" success "ok" "f"); codigo=$?
confere "sem webhook pula sem falhar" '[[ $codigo -eq 0 && ! -e "$FALSO_ULTIMA" && "$saida" == *"aviso pulado"* ]]'

saida=$(FALSO_SAIDA=22 DISCORD_ALERTAS=https://d/segredo "$aqui/avisar.sh" failure "ok" "f"); codigo=$?
confere "Discord fora não derruba o workflow" '[[ $codigo -eq 0 && "$saida" == *"não saiu"* ]]'
confere "a URL do webhook não aparece na saída" '[[ "$saida" != *segredo* ]]'

exit "$falhas"
