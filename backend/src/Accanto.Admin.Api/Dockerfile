# syntax=docker/dockerfile:1.6
# Monorepo build context = repo root (come backend/Dockerfile e frontend/Dockerfile).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/Accanto.slnx ./Accanto.slnx
COPY backend/src/Accanto.Admin.Domain/Accanto.Admin.Domain.csproj ./src/Accanto.Admin.Domain/
COPY backend/src/Accanto.Admin.Application/Accanto.Admin.Application.csproj ./src/Accanto.Admin.Application/
COPY backend/src/Accanto.Admin.Infrastructure/Accanto.Admin.Infrastructure.csproj ./src/Accanto.Admin.Infrastructure/
COPY backend/src/Accanto.Admin.Api/Accanto.Admin.Api.csproj ./src/Accanto.Admin.Api/
RUN dotnet restore ./src/Accanto.Admin.Api/Accanto.Admin.Api.csproj

COPY backend/ ./
# APP_VERSION arriva dal workflow (tag git). Default 0.0.0 per build locale.
ARG APP_VERSION=0.0.0
RUN dotnet publish ./src/Accanto.Admin.Api/Accanto.Admin.Api.csproj -c Release -o /app/publish /p:UseAppHost=false /p:Version=${APP_VERSION}

# Runtime chiseled distroless (utente non-root `app`, UID 1654), come il backend pubblico.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build --chown=app:app /app/publish /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Accanto.Admin.Api.dll"]
