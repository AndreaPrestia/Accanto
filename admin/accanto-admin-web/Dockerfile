# syntax=docker/dockerfile:1.6
# Build context = repo root (monorepo), come frontend/Dockerfile. L'admin-web
# NON usa packages/shared, ma il context root mantiene la coerenza col workflow.
FROM node:26-alpine AS build
WORKDIR /repo

COPY admin/accanto-admin-web/package.json admin/accanto-admin-web/package-lock.json* ./admin/accanto-admin-web/
RUN rm -f admin/accanto-admin-web/package-lock.json && \
    npm install --no-audit --no-fund --prefix admin/accanto-admin-web

COPY admin/accanto-admin-web ./admin/accanto-admin-web

ARG VITE_ADMIN_API_BASE_URL=/admin-api
ENV VITE_ADMIN_API_BASE_URL=$VITE_ADMIN_API_BASE_URL

RUN npm run build --prefix admin/accanto-admin-web

# Immagine unprivileged (utente nginx UID 101), come il frontend pubblico.
FROM nginxinc/nginx-unprivileged:1.31-alpine
USER root
RUN apk upgrade --no-cache
COPY --from=build /repo/admin/accanto-admin-web/dist /usr/share/nginx/html
COPY admin/accanto-admin-web/nginx.conf /etc/nginx/conf.d/default.conf
COPY admin/accanto-admin-web/security-headers.conf /etc/nginx/conf.d/security-headers.conf
USER nginx
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q -O - http://127.0.0.1:8080/ >/dev/null || exit 1
