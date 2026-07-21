FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj", "src/Bff/SystemDashboard.Bff/"]
COPY ["src/Shared/Infrastructure/His.Hope.Infrastructure/His.Hope.Infrastructure.csproj", "src/Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["src/Bff/His.Hope.Bff.Core/His.Hope.Bff.Core.csproj", "src/Bff/His.Hope.Bff.Core/"]
RUN dotnet restore "src/Bff/SystemDashboard.Bff/SystemDashboard.Bff.csproj"
COPY . .
WORKDIR "/src/src/Bff/SystemDashboard.Bff"
RUN dotnet publish "SystemDashboard.Bff.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS final
WORKDIR /app
EXPOSE 5700
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SystemDashboard.Bff.dll"]
