FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/ClinicalService/ClinicalService.Api/", "Services/ClinicalService/ClinicalService.Api/"]
COPY ["src/Services/ClinicalService/ClinicalService.Domain/", "Services/ClinicalService/ClinicalService.Domain/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "Services/ClinicalService/ClinicalService.Api/ClinicalService.Api.csproj"
RUN dotnet publish "Services/ClinicalService/ClinicalService.Api/ClinicalService.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClinicalService.Api.dll"]
