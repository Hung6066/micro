FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /src
COPY . .
RUN dotnet publish "src/Bff/${PROJECT}/${PROJECT}.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS final
ARG PROJECT
WORKDIR /app
COPY --from=build /app/publish .
RUN printf '#!/bin/sh\nexec dotnet /app/%s.dll\n' "$PROJECT" > /entrypoint.sh && chmod 0555 /entrypoint.sh
ENTRYPOINT ["/entrypoint.sh"]
