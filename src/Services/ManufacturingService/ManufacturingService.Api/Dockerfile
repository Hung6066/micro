FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:738f72aff955d5c5690e06b6fc8ecd1b90ead564099a257ac528ff720b8da85f AS build
WORKDIR /src
COPY ["Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj", "Services/ManufacturingService/ManufacturingService.Api/"]
COPY ["Services/ManufacturingService/ManufacturingService.Domain/ManufacturingService.Domain.csproj", "Services/ManufacturingService/ManufacturingService.Domain/"]
COPY ["Services/ManufacturingService/ManufacturingService.Application/ManufacturingService.Application.csproj", "Services/ManufacturingService/ManufacturingService.Application/"]
COPY ["Services/ManufacturingService/ManufacturingService.Infrastructure/ManufacturingService.Infrastructure.csproj", "Services/ManufacturingService/ManufacturingService.Infrastructure/"]
COPY . .
RUN dotnet restore "Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj"
RUN dotnet publish "Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy@sha256:3aa3a0b03b4f8f3806b5f3ff5e7c51a1e7939452fe2cf8ff07c1c413d8dc3ae2 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5050
EXPOSE 5050
COPY --from=build /app/publish .
# Recreate this small text manifest in the final layer. The self-hosted Docker
# builder can zero-fill the generated runtimeconfig file while streaming a
# compose build context, which makes the otherwise valid image fail before the
# application starts.
RUN printf '%s\n' '{"runtimeOptions":{"tfm":"net8.0","frameworks":[{"name":"Microsoft.NETCore.App","version":"8.0.0"},{"name":"Microsoft.AspNetCore.App","version":"8.0.0"}]}}' > /app/ManufacturingService.Api.runtimeconfig.json
ENTRYPOINT ["dotnet", "ManufacturingService.Api.dll"]
