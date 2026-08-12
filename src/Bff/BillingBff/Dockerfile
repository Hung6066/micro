FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e2f26f26169fd10d6f1b426e01c97397717b32e9d5ab4ee4a7d5497ed9403007 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Bff/BillingBff/BillingBff.csproj"
RUN dotnet publish "src/Bff/BillingBff/BillingBff.csproj" -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra@sha256:9c681b562bfe4e55c806ba768543b3f9711ba85afc23923548d198dc30b8e898 AS final
WORKDIR /app
EXPOSE 5600
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "BillingBff.dll"]
