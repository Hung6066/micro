FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:738f72aff955d5c5690e06b6fc8ecd1b90ead564099a257ac528ff720b8da85f AS build
WORKDIR /src
COPY ["Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj", "Services/ManufacturingService/ManufacturingService.Api/"]
COPY ["Services/ManufacturingService/ManufacturingService.Domain/ManufacturingService.Domain.csproj", "Services/ManufacturingService/ManufacturingService.Domain/"]
COPY ["Services/ManufacturingService/ManufacturingService.Application/ManufacturingService.Application.csproj", "Services/ManufacturingService/ManufacturingService.Application/"]
COPY ["Services/ManufacturingService/ManufacturingService.Infrastructure/ManufacturingService.Infrastructure.csproj", "Services/ManufacturingService/ManufacturingService.Infrastructure/"]
COPY . .
RUN dotnet restore "Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj"
RUN dotnet publish "Services/ManufacturingService/ManufacturingService.Api/ManufacturingService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy@sha256:5cd87b27e31ced7711a98c90d2f445ef594b8387ffe8616fe513c9f2ddd66dd0 AS final
WORKDIR /app
# Pull OS security fixes published after the base image; build nodes allow only HTTPS egress.
RUN find /etc/apt/sources.list /etc/apt/sources.list.d -type f -exec sed -i 's|http://|https://|g' {} + && apt-get update && apt-get upgrade -y --no-install-recommends && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:5050
EXPOSE 5050
COPY --from=build /app/publish .
# Recreate this small text manifest in the final layer. The self-hosted Docker
# builder can zero-fill the generated runtimeconfig file while streaming a
# compose build context, which makes the otherwise valid image fail before the
# application starts.
RUN printf '%s\n' '{"runtimeOptions":{"tfm":"net8.0","frameworks":[{"name":"Microsoft.NETCore.App","version":"8.0.0"},{"name":"Microsoft.AspNetCore.App","version":"8.0.0"}]}}' > /app/ManufacturingService.Api.runtimeconfig.json
ENTRYPOINT ["dotnet", "ManufacturingService.Api.dll"]
