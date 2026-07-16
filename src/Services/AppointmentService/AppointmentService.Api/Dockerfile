FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/AppointmentService/AppointmentService.Api/", "Services/AppointmentService/AppointmentService.Api/"]
COPY ["src/Services/AppointmentService/AppointmentService.Application/", "Services/AppointmentService/AppointmentService.Application/"]
COPY ["src/Services/AppointmentService/AppointmentService.Domain/", "Services/AppointmentService/AppointmentService.Domain/"]
COPY ["src/Services/AppointmentService/AppointmentService.Infrastructure/", "Services/AppointmentService/AppointmentService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "Services/AppointmentService/AppointmentService.Api/AppointmentService.Api.csproj"
RUN dotnet publish "Services/AppointmentService/AppointmentService.Api/AppointmentService.Api.csproj" -c Release -o /app/publish

FROM curlimages/curl:8.12.1 AS curl

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
COPY --from=curl /usr/bin/curl /usr/bin/curl
# Chiseled images use 'app' user (UID 1654) — no shell, no package manager
# No RUN adduser needed; user already exists with correct permissions
USER app
ENTRYPOINT ["dotnet", "AppointmentService.Api.dll"]
