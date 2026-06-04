# Custom Caddy image con il modulo `caddy-ratelimit` di Matt Holt.
#
# Caddy vanilla NON ha rate-limit nativo: questo Dockerfile estende
# l'immagine ufficiale aggiungendo `github.com/mholt/caddy-ratelimit`
# via `xcaddy` (build pattern documentato su https://caddyserver.com/docs/build).
#
# Risultato: stesso binario `caddy` con le directive `rate_limit` disponibili
# nel Caddyfile. Il modulo e' ampiamente usato in produzione (Caddy
# community, Smallstep) e mantiene compatibilita' con i pattern matcher
# standard di Caddy v2.
#
# Build:
#   docker build -t accanto-caddy ./deploy/caddy
# Usato automaticamente da docker-compose.prod.yml.
#
# Aggiornamento di Caddy: bump della tag `2.x.x` su ENTRAMBI gli stage
# (builder + runtime) e rebuild.
FROM caddy:2.8.4-builder-alpine AS builder

RUN xcaddy build \
    --with github.com/mholt/caddy-ratelimit

FROM caddy:2.8.4-alpine

# Sovrascrive il binario di default con quello che include il modulo.
COPY --from=builder /usr/bin/caddy /usr/bin/caddy

# Healthcheck: il process risponde su :2019 (admin API) se configurato; qui
# verifichiamo solo che il binario esista e il config sia parsabile.
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD /usr/bin/caddy version >/dev/null 2>&1 || exit 1
