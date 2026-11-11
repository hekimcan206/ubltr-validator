# syntax=docker/dockerfile:1

# ---- build -------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore in its own layer: it only re-runs when package versions or project files change.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY build/ build/
COPY src/UblTr.Validator/UblTr.Validator.csproj src/UblTr.Validator/
COPY src/UblTr.Api/UblTr.Api.csproj src/UblTr.Api/
COPY src/UblTr.Cli/UblTr.Cli.csproj src/UblTr.Cli/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/UblTr.Api/UblTr.Api.csproj && dotnet restore src/UblTr.Cli/UblTr.Cli.csproj

COPY .editorconfig ./
COPY src/ src/
# The publish step downloads the GIB packages (hash-verified) and copies them into the output's gib/ folder.
# A cache mount keeps them between builds so ebelge.gib.gov.tr is not hit on every image build.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    --mount=type=cache,id=ubltr-gib,target=/src/.gib \
    dotnet publish src/UblTr.Api/UblTr.Api.csproj --configuration Release --no-restore --output /app/api -p:UseAppHost=false \
 && dotnet publish src/UblTr.Cli/UblTr.Cli.csproj --configuration Release --no-restore --output /app/cli -p:UseAppHost=false

# ---- runtime -----------------------------------------------------------------------------------------
# Chiseled Ubuntu: no shell, no package manager, and a non-root "app" user (UID 1654) baked in.
# The image contains the GIB XSDs and code lists fetched at build time; they are not redistributable, so build the
# image yourself and keep it in a private registry (docs/gib-package.md).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
LABEL org.opencontainers.image.title="ubltr-validator" \
      org.opencontainers.image.description="UBL-TR (e-Fatura / e-Arsiv) validator: HTTP API and CLI" \
      org.opencontainers.image.source="https://github.com/hekimcan206/ubltr-validator" \
      org.opencontainers.image.licenses="MIT"
WORKDIR /app
COPY --from=build --link /app/api ./
COPY --from=build --link /app/cli ./cli/
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "UblTr.Api.dll"]
