FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e2f26f26169fd10d6f1b426e01c97397717b32e9d5ab4ee4a7d5497ed9403007 AS build
WORKDIR /src
COPY ["Services/CommerceService/CommerceService.Api/CommerceService.Api.csproj", "Services/CommerceService/CommerceService.Api/"]
COPY . .
RUN dotnet restore "Services/CommerceService/CommerceService.Api/CommerceService.Api.csproj"
RUN dotnet publish "Services/CommerceService/CommerceService.Api/CommerceService.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra@sha256:4b3c234b8d8bd1dd2691482fe0c103644f0334ef9727e5ecddd87d9175116dbd AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5015
EXPOSE 5015
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CommerceService.Api.dll"]
