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

# Runtime: chiseled distroless di Microsoft.
# - no shell, no package manager (zero CVE da apt/distro)
# - utente non-root (`app`, UID 1654) gia' configurato
# - solo le dipendenze ASP.NET, niente curl/wget
# Nota: lo storage scrivibile (/data/storage) arriva via bind/volume in compose;
# l'utente `app` deve poter scrivere sul mount.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build --chown=app:app /app/publish /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Accanto.Api.dll"]
