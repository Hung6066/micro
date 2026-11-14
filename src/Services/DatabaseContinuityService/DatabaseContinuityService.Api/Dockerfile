FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e2f26f26169fd10d6f1b426e01c97397717b32e9d5ab4ee4a7d5497ed9403007 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Services/DatabaseContinuityService/DatabaseContinuityService.Api/DatabaseContinuityService.Api.csproj"
RUN dotnet publish "src/Services/DatabaseContinuityService/DatabaseContinuityService.Api/DatabaseContinuityService.Api.csproj" -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy@sha256:5cd87b27e31ced7711a98c90d2f445ef594b8387ffe8616fe513c9f2ddd66dd0 AS final
WORKDIR /app
EXPOSE 8080
USER root
# The continuity executor is intentionally shipped with the service image so
# backup/PITR jobs cannot silently point at a missing host script. Production
# still runs it with least-privilege database credentials and isolated targets.
RUN apt-get update \
    && apt-get upgrade -y --no-install-recommends \
    && apt-get install -y --no-install-recommends bash curl jq postgresql-client \
    && rm -rf /var/lib/apt/lists/*
COPY scripts/database-continuity-executor.sh /opt/his-hope/bin/database-continuity-executor.sh
RUN sed -i 's/\r$//' /opt/his-hope/bin/database-continuity-executor.sh \
    && chmod 0555 /opt/his-hope/bin/database-continuity-executor.sh
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "DatabaseContinuityService.Api.dll"]
