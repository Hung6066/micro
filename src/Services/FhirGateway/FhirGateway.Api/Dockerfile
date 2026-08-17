FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e2f26f26169fd10d6f1b426e01c97397717b32e9d5ab4ee4a7d5497ed9403007 AS build
WORKDIR /src

COPY ["src/Services/FhirGateway/FhirGateway.Api/*.csproj", "Services/FhirGateway/FhirGateway.Api/"]
COPY ["src/Services/FhirGateway/FhirGateway.Application/*.csproj", "Services/FhirGateway/FhirGateway.Application/"]
COPY ["src/Services/FhirGateway/FhirGateway.Domain/*.csproj", "Services/FhirGateway/FhirGateway.Domain/"]
COPY ["src/Shared/", "Shared/"]
RUN dotnet restore "Services/FhirGateway/FhirGateway.Api/FhirGateway.Api.csproj"

COPY ["src/Services/FhirGateway/FhirGateway.Api/", "Services/FhirGateway/FhirGateway.Api/"]
COPY ["src/Services/FhirGateway/FhirGateway.Application/", "Services/FhirGateway/FhirGateway.Application/"]
COPY ["src/Services/FhirGateway/FhirGateway.Domain/", "Services/FhirGateway/FhirGateway.Domain/"]
COPY ["src/Shared/", "Shared/"]
RUN dotnet publish "Services/FhirGateway/FhirGateway.Api/FhirGateway.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra@sha256:5ca699b92bdafde798d1177500586f915ff49b3c12cac77a27dbcdbcf0e760c6 AS final
WORKDIR /app
EXPOSE 5040
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_gcServer=1
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "FhirGateway.Api.dll"]
