FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EnrichmentService/EnrichmentService.csproj ./EnrichmentService/
RUN dotnet restore ./EnrichmentService/EnrichmentService.csproj

COPY EnrichmentService/ ./EnrichmentService/
RUN dotnet publish ./EnrichmentService/EnrichmentService.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser

RUN mkdir -p /app/logs && chown appuser:appuser /app/logs

COPY --from=build /app/publish .

USER appuser

EXPOSE 9090

ENTRYPOINT ["dotnet", "EnrichmentService.dll"]