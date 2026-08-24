FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ManufacturingService.Api.csproj", "."]
RUN dotnet restore "ManufacturingService.Api.csproj"
COPY . .
RUN dotnet publish "ManufacturingService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5050
EXPOSE 5050
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ManufacturingService.Api.dll"]
