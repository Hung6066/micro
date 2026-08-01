# syntax=docker/dockerfile:1

# ============================================================
# BUILD STAGE
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# --- Restore dependencies (layer cached) ---
# Copy ONLY .csproj files first for layer caching
COPY ["src/ApiGateway/*.csproj", "ApiGateway/"]
COPY ["src/Bff/His.Hope.Bff.Core/*.csproj", "Bff/His.Hope.Bff.Core/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/*.csproj", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
COPY ["src/Shared/Infrastructure/His.Hope.Infrastructure/*.csproj", "Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["src/Shared/AspNetCore/His.Hope.AspNetCore/*.csproj", "Shared/AspNetCore/His.Hope.AspNetCore/"]
COPY ["src/Shared/Contracts/His.Hope.Contracts/*.csproj", "Shared/Contracts/His.Hope.Contracts/"]
COPY ["src/Shared/Messaging/His.Hope.Messaging.Abstractions/*.csproj", "Shared/Messaging/His.Hope.Messaging.Abstractions/"]
COPY ["src/Shared/Messaging/His.Hope.Messaging.Redis/*.csproj", "Shared/Messaging/His.Hope.Messaging.Redis/"]
COPY ["src/Shared/Messaging/His.Hope.Messaging.Sql/*.csproj", "Shared/Messaging/His.Hope.Messaging.Sql/"]
COPY ["src/Shared/Messaging/His.Hope.Messaging.RabbitMq/*.csproj", "Shared/Messaging/His.Hope.Messaging.RabbitMq/"]
COPY ["src/Shared/Observability/His.Hope.Observability/*.csproj", "Shared/Observability/His.Hope.Observability/"]
COPY ["src/Shared/Observability/His.Hope.Observability.OpenTelemetry/*.csproj", "Shared/Observability/His.Hope.Observability.OpenTelemetry/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBus/*.csproj", "Shared/EventBus/Src/His.Hope.EventBus/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/*.csproj", "Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/"]
COPY ["src/Shared/EventBus/Src/His.Hope.IntegrationEvents/*.csproj", "Shared/EventBus/Src/His.Hope.IntegrationEvents/"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "ApiGateway/ApiGateway.csproj"

# --- Copy source and publish ---
COPY ["src/ApiGateway/", "ApiGateway/"]
COPY ["src/Bff/His.Hope.Bff.Core/", "Bff/His.Hope.Bff.Core/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
COPY ["src/Shared/Infrastructure/His.Hope.Infrastructure/", "Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["src/Shared/AspNetCore/His.Hope.AspNetCore/", "Shared/AspNetCore/His.Hope.AspNetCore/"]
COPY ["src/Shared/Contracts/His.Hope.Contracts/", "Shared/Contracts/His.Hope.Contracts/"]
COPY ["src/Shared/Messaging/His.Hope.Messaging.Abstractions/", "Shared/Messaging/His.Hope.Messaging.Abstractions/"]
COPY ["src/Shared/Messaging/His.Hope.Messaging.Redis/", "Shared/Messaging/His.Hope.Messaging.Redis/"]
COPY ["src/Shared/Messaging/His.Hope.Messaging.Sql/", "Shared/Messaging/His.Hope.Messaging.Sql/"]
COPY ["src/Shared/Messaging/His.Hope.Messaging.RabbitMq/", "Shared/Messaging/His.Hope.Messaging.RabbitMq/"]
COPY ["src/Shared/Observability/His.Hope.Observability/", "Shared/Observability/His.Hope.Observability/"]
COPY ["src/Shared/Observability/His.Hope.Observability.OpenTelemetry/", "Shared/Observability/His.Hope.Observability.OpenTelemetry/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBus/", "Shared/EventBus/Src/His.Hope.EventBus/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/", "Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/"]
COPY ["src/Shared/EventBus/Src/His.Hope.IntegrationEvents/", "Shared/EventBus/Src/His.Hope.IntegrationEvents/"]
COPY ["src/Shared/Protos/", "Shared/Protos/"]

RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "ApiGateway/ApiGateway.csproj" \
    -c Release \
    -o /app/publish


# ============================================================
# RUNTIME STAGE (distroless chiseled-extra)
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra AS runtime
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

# Chiseled images use 'app' user (UID 1654) — no shell, no package manager
USER app

HEALTHCHECK NONE

ENTRYPOINT ["dotnet", "ApiGateway.dll"]
