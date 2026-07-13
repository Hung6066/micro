FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/AppointmentService/AppointmentService.Api/", "Services/AppointmentService/AppointmentService.Api/"]
COPY ["src/Services/AppointmentService/AppointmentService.Domain/", "Services/AppointmentService/AppointmentService.Domain/"]
COPY ["src/Shared/SharedKernel/Src/His.Hope.SharedKernel/", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
RUN dotnet restore "Services/AppointmentService/AppointmentService.Api/AppointmentService.Api.csproj"
RUN dotnet publish "Services/AppointmentService/AppointmentService.Api/AppointmentService.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AppointmentService.Api.dll"]
