# syntax=docker/dockerfile:1

# ============================================================
# BUILD STAGE
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# --- Restore dependencies (layer cached) ---
COPY ["src/Services/RemediationOperator/*.csproj", "Services/RemediationOperator/"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "Services/RemediationOperator/RemediationOperator.csproj"

# --- Copy source and publish ---
COPY ["src/Services/RemediationOperator/", "Services/RemediationOperator/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "Services/RemediationOperator/RemediationOperator.csproj" \
    -c Release \
    -o /app/publish \
    

# ============================================================
# CURL STAGE (for healthchecks)
# ============================================================
FROM curlimages/curl:8.12.1 AS curl

# ============================================================
# RUNTIME STAGE (distroless chiseled)
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
EXPOSE 9090

# Runtime environment defaults
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_gcServer=1

COPY --from=build /app/publish .
COPY --from=curl /usr/bin/curl /usr/bin/curl

# Create app user and log directory
RUN apt-get update && apt-get install -y curl && \
    adduser --disabled-password --gecos '' appuser && \
    mkdir -p /var/log/remediation-operator && \
    chown -R appuser:appuser /app /var/log/remediation-operator
USER appuser

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD ["curl", "-f", "-s", "-o", "/dev/null", "http://localhost:8080/health"]

ENTRYPOINT ["dotnet", "RemediationOperator.dll"]
