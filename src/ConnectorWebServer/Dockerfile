# ============================================================
# CodeCliConnector Server - Docker Build
# Multi-stage: build AOT binary on SDK image, run on minimal runtime
# ============================================================

# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/nightly/sdk:10.0 AS build

# Install native dependencies for AOT publishing
RUN apt-get update && \
    apt-get install -y --no-install-recommends clang zlib1g-dev && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /repo

# Copy NuGet config first (custom package sources)
COPY nuget.config ./

# Copy project files for restore
COPY src/Directory.Build.props src/Directory.Packages.props src/.editorconfig src/
COPY src/Utilities/SqliteGenerator/SqliteGenerator.csproj src/Utilities/SqliteGenerator/
COPY src/ConnectorShare/ConnectorShare.csproj src/ConnectorShare/
COPY src/ConnectorStorage/ConnectorStorage.csproj src/ConnectorStorage/
COPY src/ConnectorWebServer/ConnectorWebServer.csproj src/ConnectorWebServer/

# Restore dependencies
RUN dotnet restore src/ConnectorWebServer/ConnectorWebServer.csproj -r linux-x64

# Copy all source code
COPY src/Utilities/SqliteGenerator/ src/Utilities/SqliteGenerator/
COPY src/ConnectorShare/ src/ConnectorShare/
COPY src/ConnectorStorage/ src/ConnectorStorage/
COPY src/ConnectorWebServer/ src/ConnectorWebServer/

# Publish AOT binary
RUN dotnet publish src/ConnectorWebServer/ConnectorWebServer.csproj \
    -c Release \
    -r linux-x64 \
    -o /app/publish

# --- Stage 2: Runtime ---
FROM mcr.microsoft.com/dotnet/nightly/runtime-deps:10.0 AS runtime

# Labels
LABEL org.opencontainers.image.title="CodeCliConnector Server" \
      org.opencontainers.image.description="Relay server for remotely controlling Claude Code instances from mobile devices" \
      org.opencontainers.image.source="https://github.com/richasy/CodeCliConnector" \
      org.opencontainers.image.licenses="MIT"

WORKDIR /app

# Create data directory for SQLite database
RUN mkdir -p /app/data

# Copy the AOT-compiled binary
COPY --from=build /app/publish .

# Default environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ServerSettings__DatabasePath=/app/data/connector.db \
    ServerSettings__PreSharedKey=change-me-in-production

EXPOSE 8080

VOLUME ["/app/data"]

ENTRYPOINT ["./ConnectorWebServer"]
