#!/usr/bin/env bash
# Roda localmente o que a CI rodaria na branch (deploy-ci §3.1), para o PR chegar verde e gastar um run só.
# As áreas seguem o filtro do job "O que mudou" do ci.yml: mudou uma regra lá, mude aqui (e no ci-local.test.sh).
#
# Uso: scripts/ci-local.sh [--rapido] [--tudo] [--areas]
#   (sem opção)  áreas da diferença para a main (origin/main, ou CI_LOCAL_BASE), com os testes completos
#   --rapido     o mesmo, sem bundle, integração, funcionais e build do front (é o que o pre-push usa)
#   --tudo       todas as áreas, sem olhar a diferença
#   --areas      só imprime as áreas da lista de arquivos lida da entrada padrão (usado pelos testes)
# E2E e imagem ficam para a CI: precisam do banco, da API no ar e do Docker.
set -euo pipefail

rapido=false
tudo=false
so_areas=false
for arg in "$@"; do
    case "$arg" in
        --rapido) rapido=true ;;
        --tudo) tudo=true ;;
        --areas) so_areas=true ;;
        -h | --help) sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "Opção desconhecida: $arg" >&2; exit 2 ;;
    esac
done

# Lista de arquivos (um por linha) → áreas, uma por linha, na ordem fixa.
areas_de() {
    local backend=false web=false acoes=false specs=false borda=false arquivo
    while IFS= read -r arquivo; do
        [[ -z "$arquivo" ]] && continue
        case "$arquivo" in
            .github/workflows/ci.yml | .github/actions/subir-acordu/* | .github/actions/cache-nuget/* | .editorconfig | .gitattributes)
                backend=true; web=true; borda=true ;;
        esac
        case "$arquivo" in
            src/* | tests/* | global.json | .config/* | openapi/* | scripts/bundle-migrations.sh) backend=true ;;
            Directory.*.props) backend=true ;;
            *.sln) [[ "$arquivo" != */* ]] && backend=true ;;
        esac
        case "$arquivo" in
            apps/web/* | openapi/*) web=true ;;
        esac
        case "$arquivo" in
            .github/actions/avisar-discord/* | .github/workflows/ci.yml) acoes=true ;;
        esac
        case "$arquivo" in
            docs/specs/* | scripts/conferir-* | scripts/rotulos* | scripts/ci-local* | docs/processo.md | .claude/* | .github/workflows/ci.yml)
                specs=true ;;
        esac
        case "$arquivo" in
            infra/cloudflare/*) borda=true ;;
        esac
    done
    $backend && echo backend
    $web && echo web
    $acoes && echo acoes
    $specs && echo specs
    $borda && echo borda
    return 0
}

if $so_areas; then
    areas_de
    exit 0
fi

raiz="$(git rev-parse --show-toplevel)"
cd "$raiz"

if $tudo; then
    areas=$'backend\nweb\nacoes\nspecs\nborda'
else
    base="${CI_LOCAL_BASE:-origin/main}"
    if ! git rev-parse --verify -q "$base" > /dev/null; then
        echo "Sem $base para comparar; rode git fetch origin main ou use --tudo." >&2
        exit 2
    fi
    inicio="$(git merge-base "$base" HEAD)"
    # O que a branch commitou e o que ainda está na árvore de trabalho.
    areas="$( { git diff --name-only "$inicio" HEAD; git diff --name-only HEAD; git ls-files --others --exclude-standard; } | sort -u | areas_de)"
fi

if [[ -z "$areas" ]]; then
    echo "Nada que a CI rode mudou (só documentos?)."
    exit 0
fi

ok=()
falhou=()
pulou=()

passo() {
    local nome="$1"
    shift
    echo
    echo "== $nome"
    if "$@"; then ok+=("$nome"); else falhou+=("$nome"); fi
}

tem() { command -v "$1" > /dev/null 2>&1; }

backend() {
    # O bundle das migrations vem antes do restore, como na CI (#180); no --rapido, fica para ela.
    if ! $rapido; then bash scripts/bundle-migrations.sh "${TMPDIR:-/tmp}/acordu-efbundle" || return 1; fi
    dotnet restore Acordu.sln &&
        dotnet build Acordu.sln --configuration Release --no-restore &&
        if $rapido; then
            # Sem Docker: unidade e arquitetura (integração e funcionais usam Testcontainers e o banco).
            for projeto in tests/Acordu.Domain.UnitTests tests/Acordu.Application.UnitTests tests/Acordu.ArchitectureTests; do
                dotnet test "$projeto" --configuration Release --no-build || return 1
            done
        else
            dotnet test Acordu.sln --configuration Release --no-build
        fi
}

web() {
    # Encadeado com && e return: chamada dentro do if do passo, a função roda sem o set -e.
    (
        cd apps/web || exit 1
        pnpm install --frozen-lockfile --prefer-offline || exit 1
        pnpm api:tipos || exit 1
        if ! git diff --quiet -- src/shared/api; then
            echo "Tipos do OpenAPI desatualizados: commite o que o pnpm api:tipos gerou."
            exit 1
        fi
        pnpm format:check && pnpm lint && pnpm typecheck && pnpm test || exit 1
        if ! $rapido; then pnpm build && pnpm csp:conferir || exit 1; fi
    )
}

acoes() { bash .github/actions/avisar-discord/avisar.test.sh; }

specs() {
    node --test scripts/conferir-specs.test.mjs scripts/conferir-fabrica.test.mjs &&
        bash scripts/rotulos.test.sh &&
        bash scripts/ci-local.test.sh &&
        node scripts/conferir-specs.mjs
}

borda() {
    local worker
    for worker in api-borda vigia; do
        (
            cd "infra/cloudflare/$worker" &&
                pnpm install --frozen-lockfile --prefer-offline &&
                pnpm typecheck && pnpm test &&
                if ! $rapido; then pnpm exec wrangler deploy --dry-run --outdir dist; fi
        ) || return 1
    done
}

while IFS= read -r area; do
    case "$area" in
        backend) if tem dotnet; then passo "Backend (.NET)" backend; else pulou+=("Backend (sem dotnet)"); fi ;;
        web) if tem pnpm; then passo "Web (React)" web; else pulou+=("Web (sem pnpm)"); fi ;;
        acoes) passo "Ações (aviso do Discord)" acoes ;;
        specs) if tem node; then passo "Specs" specs; else pulou+=("Specs (sem node)"); fi ;;
        borda) if tem pnpm; then passo "Workers" borda; else pulou+=("Workers (sem pnpm)"); fi ;;
    esac
done <<< "$areas"

echo
echo "== Resumo$($rapido && echo ' (rápido)')"
# A forma ${a[@]+...} evita o "unbound variable" do set -u com array vazio no bash antigo (macOS).
for n in ${ok[@]+"${ok[@]}"}; do echo "ok      $n"; done
for n in ${pulou[@]+"${pulou[@]}"}; do echo "pulado  $n"; done
for n in ${falhou[@]+"${falhou[@]}"}; do echo "FALHOU  $n"; done
[[ ${#pulou[@]} -gt 0 ]] && echo "O que foi pulado fica para a CI."
[[ ${#falhou[@]} -eq 0 ]]
