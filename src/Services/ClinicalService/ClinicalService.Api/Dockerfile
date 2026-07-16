FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/ClinicalService/ClinicalService.Api/", "Services/ClinicalService/ClinicalService.Api/"]
COPY ["src/Services/ClinicalService/ClinicalService.Application/", "Services/ClinicalService/ClinicalService.Application/"]
COPY ["src/Services/ClinicalService/ClinicalService.Domain/", "Services/ClinicalService/ClinicalService.Domain/"]
COPY ["src/Services/ClinicalService/ClinicalService.Infrastructure/", "Services/ClinicalService/ClinicalService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "Services/ClinicalService/ClinicalService.Api/ClinicalService.Api.csproj"
RUN dotnet publish "Services/ClinicalService/ClinicalService.Api/ClinicalService.Api.csproj" -c Release -o /app/publish

FROM curlimages/curl:8.12.1 AS curl

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS runtime
WORKDIR /app
EXPOSE 8010
COPY --from=build /app/publish .
COPY --from=curl /usr/bin/curl /usr/bin/curl
# Chiseled images use 'app' user (UID 1654) — no shell, no package manager
# No RUN adduser needed; user already exists with correct permissions
USER app
ENTRYPOINT ["dotnet", "ClinicalService.Api.dll"]
