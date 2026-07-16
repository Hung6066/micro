FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/BillingService/BillingService.Api/", "Services/BillingService/BillingService.Api/"]
COPY ["src/Services/BillingService/BillingService.Application/", "Services/BillingService/BillingService.Application/"]
COPY ["src/Services/BillingService/BillingService.Domain/", "Services/BillingService/BillingService.Domain/"]
COPY ["src/Services/BillingService/BillingService.Infrastructure/", "Services/BillingService/BillingService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "Services/BillingService/BillingService.Api/BillingService.Api.csproj"
RUN dotnet publish "Services/BillingService/BillingService.Api/BillingService.Api.csproj" -c Release -o /app/publish

FROM curlimages/curl:8.12.1 AS curl

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS runtime
WORKDIR /app
EXPOSE 8020
COPY --from=build /app/publish .
COPY --from=curl /usr/bin/curl /usr/bin/curl
# Chiseled images use 'app' user (UID 1654) — no shell, no package manager
# No RUN adduser needed; user already exists with correct permissions
USER app
ENTRYPOINT ["dotnet", "BillingService.Api.dll"]
