FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:738f72aff955d5c5690e06b6fc8ecd1b90ead564099a257ac528ff720b8da85f AS build
WORKDIR /src
COPY ["Services/CommerceService/CommerceService.Api/CommerceService.Api.csproj", "Services/CommerceService/CommerceService.Api/"]
COPY ["Shared/AspNetCore/His.Hope.AspNetCore/His.Hope.AspNetCore.csproj", "Shared/AspNetCore/His.Hope.AspNetCore/"]
COPY ["Shared/Authorization/His.Hope.Authorization/His.Hope.Authorization.csproj", "Shared/Authorization/His.Hope.Authorization/"]
COPY ["Shared/Infrastructure/His.Hope.Infrastructure/His.Hope.Infrastructure.csproj", "Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["Shared/ServiceDefaults/His.Hope.ServiceDefaults/His.Hope.ServiceDefaults.csproj", "Shared/ServiceDefaults/His.Hope.ServiceDefaults/"]
COPY ["Shared/SharedKernel/Src/His.Hope.SharedKernel/His.Hope.SharedKernel.csproj", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
COPY ["Shared/Contracts/His.Hope.Contracts/His.Hope.Contracts.csproj", "Shared/Contracts/His.Hope.Contracts/"]
COPY . .
RUN dotnet restore "Services/CommerceService/CommerceService.Api/CommerceService.Api.csproj"
RUN dotnet publish "Services/CommerceService/CommerceService.Api/CommerceService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy@sha256:5cd87b27e31ced7711a98c90d2f445ef594b8387ffe8616fe513c9f2ddd66dd0 AS final
WORKDIR /app
# Pull OS security fixes published after the base image; build nodes allow only HTTPS egress.
RUN find /etc/apt/sources.list /etc/apt/sources.list.d -type f -exec sed -i 's|http://|https://|g' {} + && apt-get update && apt-get upgrade -y --no-install-recommends && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:5015
EXPOSE 5015
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CommerceService.Api.dll"]
