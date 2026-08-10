# syntax=docker/dockerfile:1
# Shared production build for all His.Hope domain BFFs.
# Keeping one build path prevents a shared-foundation fix from being compiled
# into only one BFF image.
ARG PROJECT=PatientBff

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet restore "src/Bff/${PROJECT}/${PROJECT}.csproj"
RUN dotnet publish "src/Bff/${PROJECT}/${PROJECT}.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS runtime
ARG PROJECT
ENV APP_PROJECT=${PROJECT}
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet ${APP_PROJECT}.dll"]
