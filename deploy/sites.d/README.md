# `sites.d/` — siti extra ospitati dallo stesso Caddy

Questa cartella esiste solo per documentare il **meccanismo**: i file di
configurazione dei siti satellite NON sono versionati qui. Vivono solo
sul server in `/opt/accanto/sites.d/*.caddy` (oppure nel repo privato
del singolo sito).

## Come funziona

1. [deploy/Caddyfile](../Caddyfile) in fondo dichiara:
   ```caddy
   import /etc/caddy/sites.d/*.caddy
   ```
2. [docker-compose.prod.yml](../../docker-compose.prod.yml) monta la
   cartella host `/opt/accanto/sites.d` come volume read-only su
   `/etc/caddy/sites.d` nel container Caddy.
3. La network Docker dello stack Accanto si chiama `edge` (vedi
   [docker-compose.yml](../../docker-compose.yml) in fondo). Stack
   docker-compose separati ci si attaccano come network esterna.

## Aggiungere un sito (workflow generico)

### A. Compose del sito (sul server, es. `/opt/<sito>/docker-compose.yml`)

```yaml
services:
  <sito>:
    image: <registry>/<owner>/<sito>:${<SITO>_VERSION:-latest}
    container_name: <sito>
    restart: unless-stopped
    pull_policy: always
    networks:
      - edge
    expose:
      - "80"          # o la porta su cui ascolta l'app
    security_opt:
      - no-new-privileges:true

networks:
  edge:
    external: true
    name: edge
```

### B. File Caddy del sito (sul server, `/opt/accanto/sites.d/<sito>.caddy`)

Template minimale per un sito statico:

```caddy
esempio.com {
    encode zstd gzip
    import security_headers     # snippet globale dal Caddyfile principale
    reverse_proxy <sito>:80
}

# Domini alternativi: redirect 308 al canonico (preserva path e query).
www.esempio.com, esempio.it, www.esempio.it {
    redir https://esempio.com{uri} 308
}
```

> Lo snippet `(security_headers)` è definito nel Caddyfile principale ed è
> globale: è accessibile anche dai file importati da `sites.d/`.
> Caddy gestisce automaticamente il challenge ACME HTTP-01 anche dentro
> blocchi con `redir` totale (i path `/.well-known/acme-challenge/*` non
> vengono mai redirezionati).

### C. DNS

Configura i record `A` di tutti i domini (canonico + alternativi) verso
l'IP del server. Se il dominio era hosted altrove (es. Squarespace),
disconnetti il sito presso il provider e rimuovi i loro record A/CNAME/HTTPS
prima di aggiungere i nuovi.

### D. Reload zero-downtime di Caddy

```bash
cd /opt/accanto/repo
docker compose -f docker-compose.yml -f docker-compose.prod.yml \
    exec caddy caddy validate --config /etc/caddy/Caddyfile
docker compose -f docker-compose.yml -f docker-compose.prod.yml \
    exec caddy caddy reload --config /etc/caddy/Caddyfile
```

### E. Avvia il container del sito

```bash
cd /opt/<sito>
docker compose pull && docker compose up -d
```

### F. Smoke test

```bash
curl -fsSI https://esempio.com/ | head -1     # 200
curl -fsSI https://esempio.it/ | head -1      # 308 -> esempio.com
```

## Troubleshooting

| Errore | Causa probabile / fix |
|---|---|
| `no such network: edge` | Lo stack Accanto non è ancora stato avviato con la nuova config network. Esegui `docker compose up -d` nel repo Accanto prima di avviare i siti satellite. |
| Caddy: `dial tcp: lookup <sito>` | Il container del sito non è `up` o non è sulla network `edge`. Verifica `docker network inspect edge` e `docker compose ps`. |
| Cert ACME fallisce | DNS non ancora propagato (`dig +short <dominio>`) o porta 80 non raggiungibile dall'esterno. Logs: `docker compose logs caddy --tail 200`. |
| Cambio config Caddy non visibile | Hai modificato solo il file in `sites.d/` ma non hai ricaricato. Il `reload` rilegge anche gli `import`. |

## Note di sicurezza

- Ogni sito decide il proprio CSP nel suo blocco (vedi i blocchi
  `{$ACCANTO_DOMAIN}` / `{$ACCANTO_APP_DOMAIN}` nel
  [Caddyfile](../Caddyfile) per esempi reali).
- Caddy ottiene/rinnova i certificati TLS automaticamente (Let's Encrypt
  HTTP-01). Il primo hit su un nuovo dominio può impiegare 5–15 secondi.
- I file in `sites.d/` non sono versionati in questo repo per evitare
  accoppiamento con progetti di terzi.
