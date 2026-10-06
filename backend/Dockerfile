# Build context: backend/ (see docker-compose.yml). Targets: `api` and `seeder`.
# Restore runs on the project files only so the layer is cached until a csproj or a package version changes.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/RequestTracker.Domain/RequestTracker.Domain.csproj src/RequestTracker.Domain/
COPY src/RequestTracker.Application/RequestTracker.Application.csproj src/RequestTracker.Application/
COPY src/RequestTracker.Infrastructure/RequestTracker.Infrastructure.csproj src/RequestTracker.Infrastructure/
COPY src/RequestTracker.Api/RequestTracker.Api.csproj src/RequestTracker.Api/
COPY tools/RequestTracker.Seeder/RequestTracker.Seeder.csproj tools/RequestTracker.Seeder/
RUN dotnet restore src/RequestTracker.Api/RequestTracker.Api.csproj \
 && dotnet restore tools/RequestTracker.Seeder/RequestTracker.Seeder.csproj

COPY src/ src/
COPY tools/ tools/
RUN dotnet publish src/RequestTracker.Api/RequestTracker.Api.csproj \
      -c Release --no-restore -o /out/api /p:UseAppHost=false \
 && dotnet publish tools/RequestTracker.Seeder/RequestTracker.Seeder.csproj \
      -c Release --no-restore -o /out/seeder /p:UseAppHost=false

# ---- Seeder: one-shot job (migrate + seed). Arguments are passed through, e.g. `--reset`.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS seeder
WORKDIR /app
COPY --from=build /out/seeder .
USER app
ENTRYPOINT ["dotnet", "RequestTracker.Seeder.dll"]
CMD ["--if-empty"]

# ---- API. curl is installed only for the Compose healthcheck (the aspnet image has none).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/api .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "RequestTracker.Api.dll"]
