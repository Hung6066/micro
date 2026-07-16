FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/PatientService/PatientService.Api/", "Services/PatientService/PatientService.Api/"]
COPY ["src/Services/PatientService/PatientService.Application/", "Services/PatientService/PatientService.Application/"]
COPY ["src/Services/PatientService/PatientService.Domain/", "Services/PatientService/PatientService.Domain/"]
COPY ["src/Services/PatientService/PatientService.Infrastructure/", "Services/PatientService/PatientService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "Services/PatientService/PatientService.Api/PatientService.Api.csproj"
RUN dotnet publish "Services/PatientService/PatientService.Api/PatientService.Api.csproj" -c Release -o /app/publish

FROM curlimages/curl:8.12.1 AS curl

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
COPY --from=curl /usr/bin/curl /usr/bin/curl
# Chiseled images use 'app' user (UID 1654) — no shell, no package manager
# No RUN adduser needed; user already exists with correct permissions
USER app
ENTRYPOINT ["dotnet", "PatientService.Api.dll"]
