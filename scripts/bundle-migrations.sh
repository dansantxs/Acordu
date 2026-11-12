#!/usr/bin/env bash
# Bundle das migrations do EF Core (deploy-ci §4, passo 3). O deploy.yml gera e executa; a CI só gera, num checkout
# limpo, para pegar o que quebraria no deploy (#180). Uso: scripts/bundle-migrations.sh <arquivo de saída>
set -euo pipefail

saida="${1:?informe o arquivo de saída do bundle}"

# O dotnet ef lê os metadados do projeto antes de compilar e precisa dos pacotes restaurados (project.assets.json).
dotnet tool restore
dotnet restore Acordu.sln
dotnet ef migrations bundle --project src/Acordu.Infrastructure --startup-project src/Acordu.Api \
  --configuration Release --output "$saida" --force
