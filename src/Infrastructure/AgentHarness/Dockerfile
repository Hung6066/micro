# syntax=docker/dockerfile:1

# ============================================================
# BUILD STAGE
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# --- Restore dependencies (layer cached) ---
COPY ["src/Infrastructure/AgentHarness/src/AgentHarness.Mcp/*.csproj", "Infrastructure/AgentHarness/src/AgentHarness.Mcp/"]
COPY ["src/Infrastructure/AgentHarness/src/AgentHarness.Application/*.csproj", "Infrastructure/AgentHarness/src/AgentHarness.Application/"]
COPY ["src/Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/*.csproj", "Infrastructure/AgentHarness/src/AgentHarness.Infrastructure/"]
COPY ["src/Infrastructure/AgentHarness/src/AgentHarness.Core/*.csproj", "Infrastructure/AgentHarness/src/AgentHarness.Core/"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "Infrastructure/AgentHarness/src/AgentHarness.Mcp/His.Hope.AgentHarness.Mcp.csproj"

# --- Copy source and publish ---
COPY ["src/Infrastructure/AgentHarness/", "Infrastructure/AgentHarness/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "Infrastructure/AgentHarness/src/AgentHarness.Mcp/His.Hope.AgentHarness.Mcp.csproj" && \
    dotnet publish "Infrastructure/AgentHarness/src/AgentHarness.Mcp/His.Hope.AgentHarness.Mcp.csproj" \
    -c Release \
    -o /app/publish

# ============================================================
# RUNTIME STAGE
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 5200

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

RUN apt-get update && apt-get install -y curl && \
    adduser --disabled-password --gecos '' appuser && \
    chown -R appuser:appuser /app
USER appuser

COPY --from=build /app/publish .
COPY src/Infrastructure/AgentHarness/workflows /app/workflows/

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
    CMD ["curl", "-f", "-s", "-o", "/dev/null", "http://localhost:5200/health"]

ENTRYPOINT ["dotnet", "His.Hope.AgentHarness.Mcp.dll"]
