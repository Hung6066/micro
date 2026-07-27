# syntax=docker/dockerfile:1

# ============================================================
# BUILD STAGE
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# --- Restore dependencies (layer cached) ---
# Copy ONLY .csproj files first for layer caching
COPY ["src/Services/IdentityService/IdentityService.Api/*.csproj", "Services/IdentityService/IdentityService.Api/"]
COPY ["src/Services/IdentityService/IdentityService.Application/*.csproj", "Services/IdentityService/IdentityService.Application/"]
COPY ["src/Services/IdentityService/IdentityService.Domain/*.csproj", "Services/IdentityService/IdentityService.Domain/"]
COPY ["src/Services/IdentityService/IdentityService.Infrastructure/*.csproj", "Services/IdentityService/IdentityService.Infrastructure/"]
COPY ["src/Bff/His.Hope.Bff.Core/*.csproj", "Bff/His.Hope.Bff.Core/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/*.csproj", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
COPY ["src/Shared/Infrastructure/His.Hope.Infrastructure/*.csproj", "Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["src/Shared/Core/His.Hope.Core/*.csproj", "Shared/Core/His.Hope.Core/"]
COPY ["src/Shared/AspNetCore/His.Hope.AspNetCore/*.csproj", "Shared/AspNetCore/His.Hope.AspNetCore/"]
COPY ["src/Shared/Contracts/His.Hope.Contracts/*.csproj", "Shared/Contracts/His.Hope.Contracts/"]
COPY ["src/Shared/Validation/His.Hope.Validation/*.csproj", "Shared/Validation/His.Hope.Validation/"]
COPY ["src/Shared/ServiceDefaults/His.Hope.ServiceDefaults/*.csproj", "Shared/ServiceDefaults/His.Hope.ServiceDefaults/"]
COPY ["src/Shared/Authorization/His.Hope.Authorization/*.csproj", "Shared/Authorization/His.Hope.Authorization/"]
COPY ["src/Shared/Resilience/His.Hope.Resilience/*.csproj", "Shared/Resilience/His.Hope.Resilience/"]
COPY ["src/Shared/Persistence/His.Hope.Persistence/*.csproj", "Shared/Persistence/His.Hope.Persistence/"]
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
    dotnet restore "Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj"

# --- Copy source and publish ---
COPY ["src/Services/IdentityService/IdentityService.Api/", "Services/IdentityService/IdentityService.Api/"]
COPY ["src/Services/IdentityService/IdentityService.Application/", "Services/IdentityService/IdentityService.Application/"]
COPY ["src/Services/IdentityService/IdentityService.Domain/", "Services/IdentityService/IdentityService.Domain/"]
COPY ["src/Services/IdentityService/IdentityService.Infrastructure/", "Services/IdentityService/IdentityService.Infrastructure/"]
COPY ["src/Bff/His.Hope.Bff.Core/", "Bff/His.Hope.Bff.Core/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
COPY ["src/Shared/Infrastructure/His.Hope.Infrastructure/", "Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["src/Shared/Core/His.Hope.Core/", "Shared/Core/His.Hope.Core/"]
COPY ["src/Shared/AspNetCore/His.Hope.AspNetCore/", "Shared/AspNetCore/His.Hope.AspNetCore/"]
COPY ["src/Shared/Contracts/His.Hope.Contracts/", "Shared/Contracts/His.Hope.Contracts/"]
COPY ["src/Shared/Validation/His.Hope.Validation/", "Shared/Validation/His.Hope.Validation/"]
COPY ["src/Shared/ServiceDefaults/His.Hope.ServiceDefaults/", "Shared/ServiceDefaults/His.Hope.ServiceDefaults/"]
COPY ["src/Shared/Authorization/His.Hope.Authorization/", "Shared/Authorization/His.Hope.Authorization/"]
COPY ["src/Shared/Resilience/His.Hope.Resilience/", "Shared/Resilience/His.Hope.Resilience/"]
COPY ["src/Shared/Persistence/His.Hope.Persistence/", "Shared/Persistence/His.Hope.Persistence/"]
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
    dotnet publish "Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj" \
    -c Release \
    -o /app/publish

# ============================================================
# RUNTIME STAGE (distroless chiseled)
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 5012

# Runtime environment defaults
# Port 8080: HTTP/REST + OIDC endpoints
# Port 5012: gRPC (for service-to-service communication)
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_gcServer=1 \
    DOTNET_GCHeapHardLimit=0x40000000 \
    DOTNET_ThreadPool_UnfairSemaphoreSpinCount=0

# ─── OpenIddict OIDC Configuration ───
ENV OpenIddict__Issuer=https://identity.his-hope.local \
    OpenIddict__AccessTokenLifetime=01:00:00 \
    OpenIddict__RefreshTokenLifetime=7.00:00:00 \
    OpenIddict__AuthorizationCodeLifetime=00:01:00

# Install curl for Docker HEALTHCHECK
COPY --from=build /app/publish .
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
USER app

# Docker HEALTHCHECK uses curl (available in jammy base image)

ENTRYPOINT ["dotnet", "IdentityService.Api.dll"]
