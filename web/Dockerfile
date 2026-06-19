FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm install --no-audit --no-fund
COPY . .
# URL pubblici del sito vetrina e dell'app SPA (sovrascrivibili da build args).
ARG SITE_URL=http://localhost:4321
ARG PUBLIC_APP_URL=http://localhost:5173
ENV SITE_URL=$SITE_URL
ENV PUBLIC_APP_URL=$PUBLIC_APP_URL
RUN npm run build

# Immagine unprivileged ufficiale: utente `nginx` (UID 101), nessun setuid,
# binding di default su porta non privilegiata 8080.
FROM nginxinc/nginx-unprivileged:1.31-alpine
USER root
# Aggiorna i pacchetti Alpine alla patch release più recente per
# rimediare alle CVE HIGH su libxml2/musl/nghttp2/zlib presenti nei layer
# di base. Eseguito al build time, niente costo runtime.
RUN apk upgrade --no-cache
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY security-headers.conf /etc/nginx/conf.d/security-headers.conf
USER nginx
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q -O - http://127.0.0.1:8080/ >/dev/null || exit 1
