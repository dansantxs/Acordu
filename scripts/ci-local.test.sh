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
confere "workers" "borda" infra/cloudflare/vigia/src/index.ts
confere "ci.yml roda tudo" "backend web acoes specs borda" .github/workflows/ci.yml
confere "ação que sobe o Acordu" "backend web borda" .github/actions/subir-acordu/action.yml
confere "editorconfig" "backend web borda" .editorconfig
confere "workflow de deploy não roda nada" "" .github/workflows/deploy.yml
confere "mistura sem repetir" "backend web" src/a.cs src/b.cs apps/web/x.ts
confere "linhas vazias ignoradas" "web" "" apps/web/x.ts ""

[[ $falhas -eq 0 ]] || { echo "$falhas falha(s)"; exit 1; }
