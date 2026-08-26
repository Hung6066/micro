FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj", "Services/ManufacturingService/ManufacturingService.Api/"]
COPY ["Services/ManufacturingService/ManufacturingService.Domain/ManufacturingService.Domain.csproj", "Services/ManufacturingService/ManufacturingService.Domain/"]
COPY ["Services/ManufacturingService/ManufacturingService.Application/ManufacturingService.Application.csproj", "Services/ManufacturingService/ManufacturingService.Application/"]
COPY ["Services/ManufacturingService/ManufacturingService.Infrastructure/ManufacturingService.Infrastructure.csproj", "Services/ManufacturingService/ManufacturingService.Infrastructure/"]
COPY . .
RUN dotnet restore "Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj"
RUN dotnet publish "Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5050
EXPOSE 5050
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ManufacturingService.Api.dll"]
