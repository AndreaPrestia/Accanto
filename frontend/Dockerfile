# Monorepo Dockerfile: il build context è la repo root (vedi docker-compose.yml),
# non la sottocartella frontend/. Questo permette di copiare anche
# packages/shared/ (workspace `@accanto/shared`) richiesto da frontend/package.json.
FROM node:26-alpine AS build
WORKDIR /repo

# 1) Copia i manifest dei workspace per massimizzare il cache layer di npm install.
COPY package.json package-lock.json* ./
COPY frontend/package.json ./frontend/
COPY packages/shared/package.json ./packages/shared/

# 2) Installa le dipendenze di tutti i workspace (incluso `@accanto/shared`
#    risolto come symlink locale).
#    Nota: il package-lock.json viene rimosso PRIMA di npm install per aggirare
#    il bug npm/cli#4828 (i pacchetti opzionali platform-specific come
#    @rollup/rollup-linux-x64-musl non vengono persistiti nel lockfile generato
#    su Windows, e su Alpine la build di Vite/Rollup fallisce).
RUN rm -f package-lock.json && \
    npm install --no-audit --no-fund --workspaces --include-workspace-root

# 3) Copia i sorgenti necessari alla build del frontend.
COPY packages/shared ./packages/shared
COPY frontend ./frontend

ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

# 4) Build solo del workspace frontend.
RUN npm run build --workspace=accanto-frontend

# Immagine unprivileged ufficiale: utente `nginx` (UID 101), nessun setuid,
# binding di default su porta non privilegiata 8080.
FROM nginxinc/nginx-unprivileged:1.31-alpine
USER root
# Aggiorna i pacchetti Alpine alla patch release più recente per
# rimediare alle CVE HIGH su libxml2/musl/nghttp2/zlib presenti nei layer
# di base. Eseguito al build time, niente costo runtime.
RUN apk upgrade --no-cache
COPY --from=build /repo/frontend/dist /usr/share/nginx/html
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY frontend/security-headers.conf /etc/nginx/conf.d/security-headers.conf
USER nginx
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD wget -q -O - http://127.0.0.1:8080/ >/dev/null || exit 1
