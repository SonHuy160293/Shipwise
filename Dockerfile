# syntax=docker/dockerfile:1.20
ARG DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b
ARG DOTNET_RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0@sha256:12b1135f5f8b3c0a214f2a680ff2033dff4f684f445d6c697d9524ceeb303586

FROM ${DOTNET_SDK_IMAGE} AS restore
WORKDIR /source
COPY global.json Directory.*.props .editorconfig Shipwise.slnx ./
COPY --parents src/**/*.csproj ./
# Keep restored packages in the layer so an imported build cache can publish offline.
ENV NUGET_HTTP_CACHE_PATH=/var/cache/nuget-http
RUN --mount=type=cache,id=shipwise-nuget-http,target=/var/cache/nuget-http \
    dotnet restore src/Modules/Shipwise.Identity/Shipwise.Identity.API/Shipwise.Identity.API.csproj && \
    dotnet restore src/Modules/Shipwise.Identity/Shipwise.Identity.MigrationRunner/Shipwise.Identity.MigrationRunner.csproj && \
    dotnet restore src/Gateway/Shipwise.Gateway/Shipwise.Gateway.csproj

FROM restore AS source
COPY src/ src/

FROM source AS publish-identity-api
RUN dotnet publish src/Modules/Shipwise.Identity/Shipwise.Identity.API/Shipwise.Identity.API.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM source AS publish-identity-migration
RUN dotnet publish src/Modules/Shipwise.Identity/Shipwise.Identity.MigrationRunner/Shipwise.Identity.MigrationRunner.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM source AS publish-gateway
RUN dotnet publish src/Gateway/Shipwise.Gateway/Shipwise.Gateway.csproj -c Release --no-restore -o /out /p:UseAppHost=false

FROM ${DOTNET_RUNTIME_IMAGE} AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID

FROM runtime AS identity-api
EXPOSE 8080
COPY --from=publish-identity-api /out .
ENTRYPOINT ["dotnet", "Shipwise.Identity.API.dll"]

FROM runtime AS identity-migration
COPY --from=publish-identity-migration /out .
ENTRYPOINT ["dotnet", "Shipwise.Identity.MigrationRunner.dll"]

FROM runtime AS gateway
EXPOSE 8080
COPY --from=publish-gateway /out .
ENTRYPOINT ["dotnet", "Shipwise.Gateway.dll"]
