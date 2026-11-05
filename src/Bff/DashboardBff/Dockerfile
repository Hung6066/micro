FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e2f26f26169fd10d6f1b426e01c97397717b32e9d5ab4ee4a7d5497ed9403007 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Bff/DashboardBff/DashboardBff.csproj"
RUN dotnet publish "src/Bff/DashboardBff/DashboardBff.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra@sha256:7a6bf765edb270c1b1443d09a76f247948e1a019b1756ebf746bb3898eaed21d AS final
WORKDIR /app
EXPOSE 5600
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "DashboardBff.dll"]
