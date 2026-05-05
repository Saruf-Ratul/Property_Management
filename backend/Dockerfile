# syntax=docker/dockerfile:1.7
# ── Build stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files first for better layer caching.
COPY PropertyManagement.sln ./
COPY src/PropertyManagement.Domain/*.csproj         src/PropertyManagement.Domain/
COPY src/PropertyManagement.Application/*.csproj    src/PropertyManagement.Application/
COPY src/PropertyManagement.Infrastructure/*.csproj src/PropertyManagement.Infrastructure/
COPY src/PropertyManagement.Api/*.csproj            src/PropertyManagement.Api/
RUN dotnet restore src/PropertyManagement.Api/PropertyManagement.Api.csproj

# Copy the rest of the source.
COPY src/ src/

RUN dotnet publish src/PropertyManagement.Api/PropertyManagement.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Non-root user for the runtime image.
RUN groupadd --system --gid 1000 appuser \
 && useradd  --system --uid 1000 --gid appuser appuser

COPY --from=build /app/publish ./
RUN mkdir -p /app/App_Data/documents /app/logs && chown -R appuser:appuser /app

USER appuser

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=1

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --retries=3 --start-period=20s \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "PropertyManagement.Api.dll"]
