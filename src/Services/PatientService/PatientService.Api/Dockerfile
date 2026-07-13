FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/PatientService/PatientService.Api/", "Services/PatientService/PatientService.Api/"]
COPY ["src/Services/PatientService/PatientService.Application/", "Services/PatientService/PatientService.Application/"]
COPY ["src/Services/PatientService/PatientService.Domain/", "Services/PatientService/PatientService.Domain/"]
COPY ["src/Services/PatientService/PatientService.Infrastructure/", "Services/PatientService/PatientService.Infrastructure/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "Services/PatientService/PatientService.Api/PatientService.Api.csproj"
RUN dotnet publish "Services/PatientService/PatientService.Api/PatientService.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PatientService.Api.dll"]
