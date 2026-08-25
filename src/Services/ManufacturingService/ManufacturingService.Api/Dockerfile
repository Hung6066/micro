FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e2f26f26169fd10d6f1b426e01c97397717b32e9d5ab4ee4a7d5497ed9403007 AS build
WORKDIR /src
COPY ["src/Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj", "Services/ManufacturingService/ManufacturingService.Api/"]
COPY ["src/Services/ManufacturingService/ManufacturingService.Domain/ManufacturingService.Domain.csproj", "Services/ManufacturingService/ManufacturingService.Domain/"]
COPY ["src/Services/ManufacturingService/ManufacturingService.Application/ManufacturingService.Application.csproj", "Services/ManufacturingService/ManufacturingService.Application/"]
COPY ["src/Services/ManufacturingService/ManufacturingService.Infrastructure/ManufacturingService.Infrastructure.csproj", "Services/ManufacturingService/ManufacturingService.Infrastructure/"]
COPY . .
COPY src/Services/ManufacturingService/ Services/ManufacturingService/
COPY src/Shared/ Shared/
RUN dotnet restore "Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj"
RUN dotnet publish "Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra@sha256:4b3c234b8d8bd1dd2691482fe0c103644f0334ef9727e5ecddd87d9175116dbd AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5050
EXPOSE 5050
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ManufacturingService.Api.dll"]
