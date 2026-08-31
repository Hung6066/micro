FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e2f26f26169fd10d6f1b426e01c97397717b32e9d5ab4ee4a7d5497ed9403007 AS build
WORKDIR /src
COPY ["Services/ContentService/ContentService.Api/ContentService.Api.csproj", "Services/ContentService/ContentService.Api/"]
COPY ["Services/ContentService/ContentService.Domain/ContentService.Domain.csproj", "Services/ContentService/ContentService.Domain/"]
COPY ["Services/ContentService/ContentService.Application/ContentService.Application.csproj", "Services/ContentService/ContentService.Application/"]
COPY ["Services/ContentService/ContentService.Infrastructure/ContentService.Infrastructure.csproj", "Services/ContentService/ContentService.Infrastructure/"]
COPY ["Shared/AspNetCore/His.Hope.AspNetCore/His.Hope.AspNetCore.csproj", "Shared/AspNetCore/His.Hope.AspNetCore/"]
COPY ["Shared/Authorization/His.Hope.Authorization/His.Hope.Authorization.csproj", "Shared/Authorization/His.Hope.Authorization/"]
COPY ["Shared/Infrastructure/His.Hope.Infrastructure/His.Hope.Infrastructure.csproj", "Shared/Infrastructure/His.Hope.Infrastructure/"]
COPY ["Shared/ServiceDefaults/His.Hope.ServiceDefaults/His.Hope.ServiceDefaults.csproj", "Shared/ServiceDefaults/His.Hope.ServiceDefaults/"]
COPY ["Shared/SharedKernel/Src/His.Hope.SharedKernel/His.Hope.SharedKernel.csproj", "Shared/SharedKernel/Src/His.Hope.SharedKernel/"]
COPY ["Shared/Contracts/His.Hope.Contracts/His.Hope.Contracts.csproj", "Shared/Contracts/His.Hope.Contracts/"]
COPY . .
RUN dotnet restore "Services/ContentService/ContentService.Api/ContentService.Api.csproj"
RUN dotnet publish "Services/ContentService/ContentService.Api/ContentService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0@sha256:b0beb9cc1dee1c1b0749796110d4734292071b814207ad0d4f40611f7db04f7b AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5016
EXPOSE 5016
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ContentService.Api.dll"]
