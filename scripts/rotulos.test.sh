#!/usr/bin/env bash
# Testes do rotulos.sh com um gh falso (P1-33): os seis rótulos do processo §7, sempre com --force (idempotente).
# Rodar: bash scripts/rotulos.test.sh
set -euo pipefail
aqui="$(cd "$(dirname "$0")" && pwd)"
falso="$(mktemp -d)"
trap 'rm -rf "$falso"' EXIT
cat > "$falso/gh" <<'SH'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$FALSO_LOG"
SH
chmod +x "$falso/gh"
export PATH="$falso:$PATH" FALSO_LOG="$falso/log"
falhas=0
confere() { if eval "$2"; then echo "ok   $1"; else echo "FALHOU $1"; falhas=$((falhas + 1)); fi; }

bash "$aqui/rotulos.sh" > /dev/null
for r in marco bug melhoria sugestao producao triagem; do
    confere "cria $r" 'grep -q "^label create $r " "$FALSO_LOG"'
done
confere "seis chamadas" '[[ $(wc -l < "$FALSO_LOG") -eq 6 ]]'
confere "sempre com --force" '[[ $(grep -c -- "--force" "$FALSO_LOG") -eq 6 ]]'

: > "$FALSO_LOG"
bash "$aqui/rotulos.sh" dono/repo > /dev/null
confere "repositório passado adiante" '[[ $(grep -c -- "--repo dono/repo" "$FALSO_LOG") -eq 6 ]]'

: > "$FALSO_LOG"
bash "$aqui/rotulos.sh" > /dev/null; bash "$aqui/rotulos.sh" > /dev/null
confere "rodar duas vezes só chama create --force (a idempotência é do --force do gh)" '[[ $(wc -l < "$FALSO_LOG") -eq 12 ]] && ! grep -qv -- "--force" "$FALSO_LOG"'

# Nomes e descrições iguais à tabela do processo §7.
lidos=0
while IFS='|' read -r _ nome descricao _; do
    nome="${nome//[\` ]/}"; descricao="$(echo "$descricao" | sed 's/^ *//; s/ *$//')"
    lidos=$((lidos + 1))
    confere "§7: $nome com a descrição do processo (sem diferenciar maiúscula)" 'grep -qi "^$nome|[0-9A-F]*|$descricao$" "$aqui/rotulos.sh"'
done < <(sed -n '/^## 7\./,/^## 8\./p' "$aqui/../docs/processo.md" | grep '^| `')
confere "a tabela do §7 tem os seis rótulos" '[[ $lidos -eq 6 ]]'

exit "$falhas"
