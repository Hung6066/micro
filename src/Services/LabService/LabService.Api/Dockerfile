# syntax=docker/dockerfile:1

# ============================================================
# BUILD STAGE
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# --- Restore dependencies (layer cached) ---
# Copy ONLY .csproj files first for layer caching
COPY ["src/Services/LabService/LabService.Api/*.csproj", "Services/LabService/LabService.Api/"]
COPY ["src/Services/LabService/LabService.Application/*.csproj", "Services/LabService/LabService.Application/"]
COPY ["src/Services/LabService/LabService.Domain/*.csproj", "Services/LabService/LabService.Domain/"]
COPY ["src/Services/LabService/LabService.Infrastructure/*.csproj", "Services/LabService/LabService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/*.csproj", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
COPY ["src/Shared/Infrastructure/His.Hope.Infrastructure/*.csproj", "Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBus/*.csproj", "Shared/EventBus/Src/His.Hope.EventBus/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/*.csproj", "Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/"]
COPY ["src/Shared/EventBus/Src/His.Hope.IntegrationEvents/*.csproj", "Shared/EventBus/Src/His.Hope.IntegrationEvents/"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "Services/LabService/LabService.Api/LabService.Api.csproj"

# --- Copy source and publish ---
COPY ["src/Services/LabService/LabService.Api/", "Services/LabService/LabService.Api/"]
COPY ["src/Services/LabService/LabService.Application/", "Services/LabService/LabService.Application/"]
COPY ["src/Services/LabService/LabService.Domain/", "Services/LabService/LabService.Domain/"]
COPY ["src/Services/LabService/LabService.Infrastructure/", "Services/LabService/LabService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
COPY ["src/Shared/Infrastructure/His.Hope.Infrastructure/", "Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBus/", "Shared/EventBus/Src/His.Hope.EventBus/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/", "Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/"]
COPY ["src/Shared/EventBus/Src/His.Hope.IntegrationEvents/", "Shared/EventBus/Src/His.Hope.IntegrationEvents/"]
COPY ["src/Shared/Protos/", "Shared/Protos/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "Services/LabService/LabService.Api/LabService.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

# ============================================================
# CURL STAGE (for healthchecks)
# ============================================================
FROM curlimages/curl:8.12.1 AS curl

# ============================================================
# RUNTIME STAGE (distroless chiseled) — URGENT FIX: was aspnet:8.0 (NOT chiseled)
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080

# Runtime environment defaults
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_gcServer=1 \
    DOTNET_GCHeapHardLimit=0x40000000 \
    DOTNET_ThreadPool_UnfairSemaphoreSpinCount=0

COPY --from=build /app/publish .
COPY --from=curl /usr/bin/curl /usr/bin/curl

# Non-root user
RUN apt-get update && apt-get install -y curl && adduser --disabled-password --gecos '' appuser && mkdir -p /app/Certificates && chown -R appuser:appuser /app
USER appuser

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD ["curl", "-f", "-s", "-o", "/dev/null", "http://localhost:8080/health"]

ENTRYPOINT ["dotnet", "LabService.Api.dll"]

