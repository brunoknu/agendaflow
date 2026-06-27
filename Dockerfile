# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AgendaFlow.slnx ./
COPY src/AgendaFlow.Domain/AgendaFlow.Domain.csproj src/AgendaFlow.Domain/
COPY src/AgendaFlow.Application/AgendaFlow.Application.csproj src/AgendaFlow.Application/
COPY src/AgendaFlow.Infrastructure/AgendaFlow.Infrastructure.csproj src/AgendaFlow.Infrastructure/
COPY src/AgendaFlow.Api/AgendaFlow.Api.csproj src/AgendaFlow.Api/
COPY Directory.Build.props ./

RUN dotnet restore src/AgendaFlow.Api/AgendaFlow.Api.csproj

COPY src/ src/

RUN dotnet publish src/AgendaFlow.Api/AgendaFlow.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "AgendaFlow.Api.dll"]
