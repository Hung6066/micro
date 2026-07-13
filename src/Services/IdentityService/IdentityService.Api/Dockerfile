FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/IdentityService/IdentityService.Api/", "Services/IdentityService/IdentityService.Api/"]
COPY ["src/Services/IdentityService/IdentityService.Application/", "Services/IdentityService/IdentityService.Application/"]
COPY ["src/Services/IdentityService/IdentityService.Domain/", "Services/IdentityService/IdentityService.Domain/"]
COPY ["src/Services/IdentityService/IdentityService.Infrastructure/", "Services/IdentityService/IdentityService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj"
RUN dotnet publish "Services/IdentityService/IdentityService.Api/IdentityService.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IdentityService.Api.dll"]
