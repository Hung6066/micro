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
