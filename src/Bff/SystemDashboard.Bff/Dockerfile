FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e2f26f26169fd10d6f1b426e01c97397717b32e9d5ab4ee4a7d5497ed9403007 AS build
# BFF JWT validation: uses JWKS from Identity Service (2026-07-23) 
WORKDIR /src
COPY ["src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj", "src/Bff/SystemDashboard.Bff/"]
COPY ["src/Shared/Infrastructure/His.Hope.Infrastructure/His.Hope.Infrastructure.csproj", "src/Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["src/Bff/His.Hope.Bff.Core/His.Hope.Bff.Core.csproj", "src/Bff/His.Hope.Bff.Core/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBus/His.Hope.EventBus.csproj", "src/Shared/EventBus/Src/His.Hope.EventBus/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/His.Hope.EventBusRabbitMQ.csproj", "src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/"]
COPY ["src/Shared/EventBus/Src/His.Hope.IntegrationEvents/His.Hope.IntegrationEvents.csproj", "src/Shared/EventBus/Src/His.Hope.IntegrationEvents/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/His.Hope.SharedKernel.csproj", "src/Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj"
COPY . .
WORKDIR "/src/src/Bff/SystemDashboard.Bff"
RUN dotnet publish "SystemDashboard.Bff.csproj" -c Release -o /app/publish 

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra@sha256:5ca699b92bdafde798d1177500586f915ff49b3c12cac77a27dbcdbcf0e760c6 AS final
WORKDIR /app
EXPOSE 5700
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SystemDashboard.Bff.dll"]
