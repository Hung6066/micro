FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/LabService/LabService.Api/", "Services/LabService/LabService.Api/"]
COPY ["src/Services/LabService/LabService.Application/", "Services/LabService/LabService.Application/"]
COPY ["src/Services/LabService/LabService.Domain/", "Services/LabService/LabService.Domain/"]
COPY ["src/Services/LabService/LabService.Infrastructure/", "Services/LabService/LabService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "Services/LabService/LabService.Api/LabService.Api.csproj"
RUN dotnet publish "Services/LabService/LabService.Api/LabService.Api.csproj" -c Release -o /app/publish

FROM curlimages/curl:8.12.1 AS curl

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS runtime
WORKDIR /app
EXPOSE 8010
COPY --from=build /app/publish .
COPY --from=curl /usr/bin/curl /usr/bin/curl
# Chiseled images use 'app' user (UID 1654) — no shell, no package manager
# No RUN adduser needed; user already exists with correct permissions
USER app
ENTRYPOINT ["dotnet", "LabService.Api.dll"]
