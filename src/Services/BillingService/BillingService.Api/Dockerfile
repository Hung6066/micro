# syntax=docker/dockerfile:1

# ============================================================
# BUILD STAGE
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# --- Restore dependencies (layer cached) ---
# Copy ONLY .csproj files first for layer caching
COPY ["src/Services/BillingService/BillingService.Api/*.csproj", "Services/BillingService/BillingService.Api/"]
COPY ["src/Services/BillingService/BillingService.Application/*.csproj", "Services/BillingService/BillingService.Application/"]
COPY ["src/Services/BillingService/BillingService.Domain/*.csproj", "Services/BillingService/BillingService.Domain/"]
COPY ["src/Services/BillingService/BillingService.Infrastructure/*.csproj", "Services/BillingService/BillingService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/*.csproj", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
COPY ["src/Shared/Infrastructure/His.Hope.Infrastructure/*.csproj", "Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBus/*.csproj", "Shared/EventBus/Src/His.Hope.EventBus/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/*.csproj", "Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/"]
COPY ["src/Shared/EventBus/Src/His.Hope.IntegrationEvents/*.csproj", "Shared/EventBus/Src/His.Hope.IntegrationEvents/"]
RUN dotnet restore "Services/BillingService/BillingService.Api/BillingService.Api.csproj"

# --- Copy source and publish ---
COPY ["src/Services/BillingService/BillingService.Api/", "Services/BillingService/BillingService.Api/"]
COPY ["src/Services/BillingService/BillingService.Application/", "Services/BillingService/BillingService.Application/"]
COPY ["src/Services/BillingService/BillingService.Domain/", "Services/BillingService/BillingService.Domain/"]
COPY ["src/Services/BillingService/BillingService.Infrastructure/", "Services/BillingService/BillingService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
COPY ["src/Shared/Infrastructure/His.Hope.Infrastructure/", "Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBus/", "Shared/EventBus/Src/His.Hope.EventBus/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/", "Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/"]
COPY ["src/Shared/EventBus/Src/His.Hope.IntegrationEvents/", "Shared/EventBus/Src/His.Hope.IntegrationEvents/"]
COPY ["src/Shared/Protos/", "Shared/Protos/"]

RUN dotnet publish "Services/BillingService/BillingService.Api/BillingService.Api.csproj" \
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

ENTRYPOINT ["dotnet", "BillingService.Api.dll"]

