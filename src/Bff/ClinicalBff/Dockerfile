FROM mcr.microsoft.com/dotnet/sdk:8.0@sha256:e2f26f26169fd10d6f1b426e01c97397717b32e9d5ab4ee4a7d5497ed9403007 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Bff/ClinicalBff/ClinicalBff.csproj"
RUN dotnet publish "src/Bff/ClinicalBff/ClinicalBff.csproj" -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-extra@sha256:5ca699b92bdafde798d1177500586f915ff49b3c12cac77a27dbcdbcf0e760c6 AS final
WORKDIR /app
EXPOSE 5600
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "ClinicalBff.dll"]
