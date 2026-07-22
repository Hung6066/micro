FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
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
RUN dotnet publish "SystemDashboard.Bff.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra AS final
WORKDIR /app
EXPOSE 5700
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SystemDashboard.Bff.dll"]
