# Imagem da API (ADR 0010, deploy-ci §2.3): linux/amd64 para o Cloud Run, ReadyToRun para o cold start e base
# aspnet "chiseled extra" (sem shell nem gerenciador de pacotes, com ICU e tzdata, exigidos pelo SLA com fusos IANA).
# Build pela raiz do repositório: docker build --platform linux/amd64 -f docker/api.Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Só os arquivos de projeto primeiro: o restore fica em cache enquanto as dependências não mudam.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Acordu.Domain/Acordu.Domain.csproj src/Acordu.Domain/
COPY src/Acordu.Application/Acordu.Application.csproj src/Acordu.Application/
COPY src/Acordu.Infrastructure/Acordu.Infrastructure.csproj src/Acordu.Infrastructure/
COPY src/Acordu.Api/Acordu.Api.csproj src/Acordu.Api/
RUN dotnet restore src/Acordu.Api/Acordu.Api.csproj -r linux-x64

COPY src/ src/
RUN dotnet publish src/Acordu.Api/Acordu.Api.csproj \
    --configuration Release --runtime linux-x64 --self-contained false --no-restore \
    -p:PublishReadyToRun=true -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=build /app .

# A imagem chiseled já roda como o usuário sem privilégios "app"; a porta é a que o Cloud Run espera.
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Acordu.Api.dll"]
