FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/PharmacyService/PharmacyService.Api/", "Services/PharmacyService/PharmacyService.Api/"]
COPY ["src/Services/PharmacyService/PharmacyService.Application/", "Services/PharmacyService/PharmacyService.Application/"]
COPY ["src/Services/PharmacyService/PharmacyService.Domain/", "Services/PharmacyService/PharmacyService.Domain/"]
COPY ["src/Services/PharmacyService/PharmacyService.Infrastructure/", "Services/PharmacyService/PharmacyService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "Services/PharmacyService/PharmacyService.Api/PharmacyService.Api.csproj"
RUN dotnet publish "Services/PharmacyService/PharmacyService.Api/PharmacyService.Api.csproj" -c Release -o /app/publish

FROM curlimages/curl:8.12.1 AS curl

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS runtime
WORKDIR /app
EXPOSE 8010
COPY --from=build /app/publish .
COPY --from=curl /usr/bin/curl /usr/bin/curl
# Chiseled images use 'app' user (UID 1654) — no shell, no package manager
# No RUN adduser needed; user already exists with correct permissions
USER app
ENTRYPOINT ["dotnet", "PharmacyService.Api.dll"]
