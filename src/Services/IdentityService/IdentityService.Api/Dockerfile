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
    -o /app/publish \
    --no-restore

# ============================================================
# RUNTIME STAGE (distroless chiseled)
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra AS final
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
USER app

# K8s probes handle health checks — no Docker HEALTHCHECK needed

ENTRYPOINT ["dotnet", "IdentityService.Api.dll"]
