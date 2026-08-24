# syntax=docker/dockerfile:1

# ============================================================
# BUILD STAGE
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e2f26f26169fd10d6f1b426e01c97397717b32e9d5ab4ee4a7d5497ed9403007 AS build
WORKDIR /src

# --- Restore dependencies ---
COPY ["src/Infrastructure/AgentHarness/src/AgentHarness.TemporalWorker/*.csproj", "Infrastructure/AgentHarness/src/AgentHarness.TemporalWorker/"]
COPY ["src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/*.csproj", "Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/"]
COPY ["src/Infrastructure/AgentHarness/src/AgentHarness.Core/*.csproj", "Infrastructure/AgentHarness/src/AgentHarness.Core/"]
RUN --mount=type=cache,id=his-hope-agent-harness-temporal-nuget,target=/root/.nuget/packages \
    dotnet restore "Infrastructure/AgentHarness/src/AgentHarness.TemporalWorker/His.Hope.AgentHarness.TemporalWorker.csproj"

# --- Copy source and publish ---
COPY ["src/Infrastructure/AgentHarness/", "Infrastructure/AgentHarness/"]

RUN --mount=type=cache,id=his-hope-agent-harness-temporal-nuget,target=/root/.nuget/packages \
    dotnet restore "Infrastructure/AgentHarness/src/AgentHarness.TemporalWorker/His.Hope.AgentHarness.TemporalWorker.csproj" && \
    dotnet publish "Infrastructure/AgentHarness/src/AgentHarness.TemporalWorker/His.Hope.AgentHarness.TemporalWorker.csproj" \
    -c Release \
    -o /app/publish

# ============================================================
# RUNTIME STAGE
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:b0beb9cc1dee1c1b0749796110d4734292071b814207ad0d4f40611f7db04f7b AS runtime
WORKDIR /app
EXPOSE 5270

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    adduser --disabled-password --gecos '' appuser && \
    chown -R appuser:appuser /app
USER appuser

COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD ["curl", "-f", "-s", "-o", "/dev/null", "http://localhost:5270/health"]

ENTRYPOINT ["dotnet", "His.Hope.AgentHarness.TemporalWorker.dll"]
