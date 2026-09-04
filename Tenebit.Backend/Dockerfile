# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Tenebit.sln && dotnet publish Tenebit.Api/Tenebit.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false /p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates && rm -rf /var/lib/apt/lists/* && mkdir -p /tmp/tenebit && chown -R app:app /tmp/tenebit
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 ASPNETCORE_ENVIRONMENT=Production DOTNET_EnableDiagnostics=0 COMPlus_EnableDiagnostics=0 HOME=/tmp/tenebit TMPDIR=/tmp
EXPOSE 8080
USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD curl --fail --silent -H "Host: teneb.it" http://127.0.0.1:8080/api/health/ready >/dev/null || exit 1
ENTRYPOINT ["dotnet", "Tenebit.Api.dll"]
