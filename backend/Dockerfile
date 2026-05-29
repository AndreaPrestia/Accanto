# syntax=docker/dockerfile:1.6
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/Accanto.slnx ./Accanto.slnx
COPY backend/src/Accanto.Domain/Accanto.Domain.csproj ./src/Accanto.Domain/
COPY backend/src/Accanto.Application/Accanto.Application.csproj ./src/Accanto.Application/
COPY backend/src/Accanto.Infrastructure/Accanto.Infrastructure.csproj ./src/Accanto.Infrastructure/
COPY backend/src/Accanto.Api/Accanto.Api.csproj ./src/Accanto.Api/
COPY backend/tests/Accanto.Tests/Accanto.Tests.csproj ./tests/Accanto.Tests/
RUN dotnet restore ./src/Accanto.Api/Accanto.Api.csproj

COPY backend/ ./
# APP_VERSION arriva dal workflow (tag git, es. "0.7.2"). Default 0.0.0 per build locale.
ARG APP_VERSION=0.0.0
RUN dotnet publish ./src/Accanto.Api/Accanto.Api.csproj -c Release -o /app/publish /p:UseAppHost=false /p:Version=${APP_VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd -m accanto \
    && mkdir -p /data/storage \
    && chown -R accanto:accanto /data/storage /app
USER accanto

COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "Accanto.Api.dll"]
