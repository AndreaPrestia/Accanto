# Runbook: log centralizzato (Seq)

Stato: stack opt-in via profilo `observability` di docker compose. In
locale e in produzione il backend continua a loggare su stdout (JSON
compatto in prod, console human-readable in dev) anche senza Seq
attivo; quando Seq e' raggiungibile, gli stessi eventi vengono
duplicati al sink Seq.

## 1. Architettura

```
backend (Serilog)
  ├─ Console sink   → stdout (sempre, indicizzato da `docker logs`)
  └─ Seq sink       → http://seq:5341 (solo se Logging__SeqUrl popolato)
                        ↓
                     datalust/seq container  (named volume `seq-data`)
                        ↓
                     UI http://localhost:5341 (in prod dietro VPN/Caddy)
```

Properties arricchite automaticamente su ogni evento (via
[`LogContextEnrichmentMiddleware`](../../backend/src/Accanto.Api/Common/LogContextEnrichmentMiddleware.cs)):

| Property | Sorgente | Esempio |
|---|---|---|
| `Application` | Serilog enricher fisso | `accanto-api` |
| `Environment` | `ASPNETCORE_ENVIRONMENT` | `Production` |
| `RequestId` | `HttpContext.TraceIdentifier` | `0HMVAB...` |
| `ClientIp` | `X-Forwarded-For` primo hop, fallback peer | `203.0.113.42` |
| `UserId` | claim `sub` / `NameIdentifier` (null se anonimo) | `0d2e...` |
| `ClientIp`, `UserAgent` (summary) | `UseSerilogRequestLogging` enricher | — |

Eventi di sicurezza custom (`Accanto.Security.Csp`, audit log, login
failed, rate-limit triggered) ereditano le stesse proprieta' → query
incrociate per IP/user senza dover passare l'ID a ogni `Log.*`.

## 2. Attivare Seq in locale

```powershell
# 1. Avvia lo stack normale + profilo observability
docker compose --profile observability up -d

# 2. In .env (o env vars del backend), abilita il sink:
#    Logging__SeqUrl=http://seq:5341
#    Logging__SeqApiKey=     # facoltativo (configurato da UI Seq)

# 3. Restart del backend per ricaricare la config:
docker compose restart backend

# 4. Apri la UI:
start http://localhost:5341
```

Al primo accesso la UI propone di creare account admin: farlo
**subito** (Seq di default non richiede auth e l'API key di ingest
puo' essere creata dopo).

## 3. Query Seq utili

| Scopo | Filtro |
|---|---|
| Login falliti per IP | `EventType = 'LoginFailed' \| select ClientIp, count() group by ClientIp` |
| Rate-limit triggered | `StatusCode = 429` |
| 5xx negli ultimi 15 min | `@Level = 'Error' and @Timestamp > Now() - 15m` |
| Attivita' di un utente | `UserId = '0d2e...'` |
| Violazioni CSP | `SourceContext = 'Accanto.Security.Csp'` |
| Slow request (>1s) | `Elapsed > 1000` |
| Errori EF | `SourceContext like 'Microsoft.EntityFrameworkCore%' and @Level >= 'Warning'` |

Salvale come **Saved Queries** in Seq → diventano dashboard cards.

## 4. Retention

Configurazione: Settings > Retention nella UI Seq. Default 30 giorni
su Seq free (1 GB indice + retention non garantita).

Per produzione raccomandato:
- 14 giorni eventi `Verbose`/`Debug`
- 90 giorni eventi `Information`+
- Retention infinita su `Error`/`Fatal` (storia incidenti)
- Backup periodico del volume `seq-data` (vedi §6)

## 5. Esposizione in produzione

**Non** esporre Seq direttamente su Internet (UI senza MFA, accesso
ai log = accesso a tutto il PII e ai token di session).

Opzioni in ordine di preferenza:

1. **VPN-only / WireGuard**: bind di Seq su `127.0.0.1:5341` sull'host
   e accesso via tunnel (`ssh -L 5341:localhost:5341 prod`).
2. **Caddy con auth basic** su sottodominio dedicato:
   ```caddy
   logs.accanto.care {
       basicauth {
           admin <bcrypt-hash>
       }
       reverse_proxy seq:80
   }
   ```
3. **OIDC** (Seq Enterprise, a pagamento).

Mai TLS off su Seq esposto pubblicamente — Caddy in mezzo termina TLS.

## 6. Backup del volume Seq

Lo stato di Seq (eventi, dashboard, API key, utenti) vive in
`/data` dentro il container = volume named `seq-data`.

```powershell
# Snapshot del volume (container stoppato per consistenza)
docker compose --profile observability stop seq
docker run --rm -v accanto_seq-data:/data -v ${PWD}/backup:/out alpine \
  tar czf /out/seq-$(Get-Date -Format yyyyMMdd).tar.gz -C / data
docker compose --profile observability start seq
```

Stesso pattern del DB backup ([backup-restore.md](backup-restore.md)).
NON e' fonte di verita': se Seq si perde si perdono solo i log
storici, l'app continua a girare e i nuovi log finiscono comunque su
stdout.

## 7. Quando NON usare Seq

- Log ad altissimo volume (>1M eventi/giorno): Seq free non basta,
  valutare Loki + Grafana o ELK.
- Compliance retention multi-anno: usare backup S3 dei log JSON
  raw da stdout (gia' raccolti da `docker logs`).

## 8. Smoke test

```powershell
# Genera un evento riconoscibile
curl -X POST http://localhost:8080/auth/login `
  -H "Content-Type: application/json" `
  -d '{"email":"smoke@test","password":"wrong"}'

# In Seq cerca: EventType = 'LoginFailed' and ClientIp = '127.0.0.1'
```

Se non appare entro pochi secondi:
1. `docker compose logs seq --tail=50` → verifica che il container sia ready
2. `docker compose logs backend | grep -i seq` → verifica che il sink sia partito senza errori (es. "Logger ... using Seq sink at http://seq:5341")
3. `docker compose exec backend wget -qO- http://seq:5341/api` → verifica raggiungibilita' network interno
