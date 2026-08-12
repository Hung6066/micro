FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e2f26f26169fd10d6f1b426e01c97397717b32e9d5ab4ee4a7d5497ed9403007 AS build
WORKDIR /src
COPY ["src/Services/ExternalIntegrationService/ExternalIntegrationService.Api/*.csproj", "Services/ExternalIntegrationService/ExternalIntegrationService.Api/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBus/*.csproj", "Shared/EventBus/Src/His.Hope.EventBus/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/*.csproj", "Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/"]
COPY ["src/Shared/EventBus/Src/His.Hope.IntegrationEvents/*.csproj", "Shared/EventBus/Src/His.Hope.IntegrationEvents/"]
COPY ["src/Shared/Messaging/His.Hope.Messaging.Abstractions/*.csproj", "Shared/Messaging/His.Hope.Messaging.Abstractions/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/*.csproj", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
COPY ["src/Shared/", "Shared/"]
RUN dotnet restore "Services/ExternalIntegrationService/ExternalIntegrationService.Api/ExternalIntegrationService.Api.csproj"
COPY ["src/Services/ExternalIntegrationService/ExternalIntegrationService.Api/", "Services/ExternalIntegrationService/ExternalIntegrationService.Api/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBus/", "Shared/EventBus/Src/His.Hope.EventBus/"]
COPY ["src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/", "Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/"]
COPY ["src/Shared/EventBus/Src/His.Hope.IntegrationEvents/", "Shared/EventBus/Src/His.Hope.IntegrationEvents/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet publish "Services/ExternalIntegrationService/ExternalIntegrationService.Api/ExternalIntegrationService.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra@sha256:5ca699b92bdafde798d1177500586f915ff49b3c12cac77a27dbcdbcf0e760c6 AS final
WORKDIR /app
EXPOSE 5060
ENV ASPNETCORE_URLS=http://+:5060 ASPNETCORE_ENVIRONMENT=Production DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "ExternalIntegrationService.Api.dll"]
