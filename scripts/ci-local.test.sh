#!/usr/bin/env bash
# Testes das áreas do ci-local.sh: as mesmas regras do filtro "O que mudou" do ci.yml.
# Rodar: bash scripts/ci-local.test.sh
set -euo pipefail
aqui="$(cd "$(dirname "$0")" && pwd)"
falhas=0
areas() { printf '%s\n' "$@" | bash "$aqui/ci-local.sh" --areas | paste -sd' ' -; }
confere() {
    local nome="$1" esperado="$2"
    shift 2
    local obtido
    obtido="$(areas "$@")"
    if [[ "$obtido" == "$esperado" ]]; then echo "ok   $nome"; else echo "FALHOU $nome: esperado '$esperado', veio '$obtido'"; falhas=$((falhas + 1)); fi
}

confere "só documentos não roda nada" "" docs/estado.md README.md docs/adr/0001-x.md
confere "código da API" "backend" src/Acordu.Domain/Duracao.cs
confere "testes" "backend" tests/Acordu.Domain.UnitTests/DuracaoTests.cs
confere "solução na raiz" "backend" Acordu.sln
confere "props e global.json" "backend" Directory.Packages.props global.json
confere "ferramentas locais" "backend" .config/dotnet-tools.json
confere "bundle das migrations" "backend" scripts/bundle-migrations.sh
confere "front" "web" apps/web/src/main.tsx
confere "contrato roda backend e front" "backend web" openapi/acordu.json
confere "aviso do Discord" "acoes" .github/actions/avisar-discord/avisar.sh
confere "specs" "specs" docs/specs/F14-lancamento-controlado/spec.md
confere "processo e agentes" "specs" docs/processo.md .claude/commands/pr.md
confere "este script" "specs" scripts/ci-local.sh
confere "fluxo BPMN e gerador" "specs" docs/fluxo-acordu.bpmn scripts/gerar-fluxo-bpmn.py
confere "workers" "borda" infra/cloudflare/vigia/src/index.ts
confere "ci.yml roda tudo" "backend web acoes specs borda" .github/workflows/ci.yml
confere "ação que sobe o Acordu" "backend web borda" .github/actions/subir-acordu/action.yml
confere "ação do cache do NuGet" "backend web borda" .github/actions/cache-nuget/action.yml
confere "editorconfig" "backend web borda" .editorconfig
confere "workflow de deploy roda as specs (F14-121)" "specs" .github/workflows/deploy.yml
confere "outro workflow não roda nada" "" .github/workflows/backup.yml
confere "mistura sem repetir" "backend web" src/a.cs src/b.cs apps/web/x.ts
confere "linhas vazias ignoradas" "web" "" apps/web/x.ts ""

# Passo do meio que falha derruba a área (a função roda dentro de if, sem set -e).
repo="$(mktemp -d)"
trap 'rm -rf "$repo"' EXIT
mkdir -p "$repo/bin" "$repo/r/apps/web"
cat > "$repo/bin/pnpm" <<'SH'
#!/usr/bin/env bash
[[ "$1" == "$PNPM_FALHA" ]] && exit 1
exit 0
SH
chmod +x "$repo/bin/pnpm"
(
    cd "$repo/r"
    git init -q && git -c user.name=t -c user.email=t@t commit -q --allow-empty -m base
    echo x > apps/web/x.ts && git add -A && git -c user.name=t -c user.email=t@t commit -q -m web
)
roda() { (cd "$repo/r" && PATH="$repo/bin:$PATH" PNPM_FALHA="$1" CI_LOCAL_BASE=HEAD~1 bash "$aqui/ci-local.sh" "${@:2}" 2>&1); }
for passo in install lint typecheck; do
    if saida="$(roda "$passo" --rapido)"; then
        echo "FALHOU falha no $passo passou"; falhas=$((falhas + 1))
    elif grep -q "FALHOU  Web" <<< "$saida"; then echo "ok   falha no $passo derruba a Web"
    else echo "FALHOU falha no $passo sem 'FALHOU  Web'"; falhas=$((falhas + 1)); fi
done
if saida="$(roda nenhum --rapido)" && grep -q "ok      Web" <<< "$saida"; then echo "ok   sem falha a Web passa"
else echo "FALHOU sem falha a Web não passou: $saida"; falhas=$((falhas + 1)); fi

[[ $falhas -eq 0 ]] || { echo "$falhas falha(s)"; exit 1; }
