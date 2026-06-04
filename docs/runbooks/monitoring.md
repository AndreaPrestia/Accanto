# Runbook — Monitoring & alerting esterno

Accanto si auto-osserva (Serilog → Seq, vedi
[logging.md](logging.md)) ma _l'auto-osservazione non basta_: se
il container backend va in crash-loop o l'host AWS è giù, il
processo che dovrebbe alertare è anche quello morto. Serve una
sonda **esterna** indipendente dall'infrastruttura monitorata.

Questo documento copre due esigenze distinte:

- **Uptime check** "tira-pull": sonda da Internet che chiama
  endpoint pubblici a intervallo regolare e alza alert se la
  risposta è KO/lenta/cambiata.
- **Dead-man's switch** "push": cron-job interni che devono
  pingare un endpoint esterno **solo quando completano con
  successo**. L'assenza del ping per N minuti = alert. Cattura
  cron silenziosamente falliti (es. backup notturno che dal
  2026-04-15 non parte più perché `BACKUP_PASSPHRASE` è stato
  rimosso dall'env).

## Provider raccomandati

Tutti hanno free tier ampio per Accanto (team piccolo, pochi
servizi). Scegliere uno, evitare lock-in pesante.

| Provider | Free tier | Adatto per | Note |
|---|---|---|---|
| [Healthchecks.io](https://healthchecks.io) | 20 check, 5 min interval | Dead-man's switch + uptime base | Self-hostable se serve sovranità dati (GDPR-friendly, ospitato in EU). Endpoint POST/GET con URL random per check. Integrazione email + Telegram + webhook gratis. |
| [UptimeRobot](https://uptimerobot.com) | 50 monitor, 5 min interval | Uptime check classico HTTP/HTTPS | Maturo, dashboard pubblica opzionale per status page. Niente dead-man's switch nativo (solo "passive monitor" a pagamento). |
| [Better Stack](https://betterstack.com/uptime) | 10 monitor, 30 sec | Uptime + status page + incident mgmt | Più completo ma free tier stretto. |

**Scelta per Accanto**: **Healthchecks.io** per i job interni
(backup, restore drill, cert renew) + **UptimeRobot** per gli
endpoint pubblici. Sono entrambi tier gratis e indipendenti, così
un outage del provider non azzera la visibilità.

## Endpoint da monitorare (uptime check)

I tre host pubblici di Accanto:

| URL | Cosa verifica | Atteso | Interval |
|---|---|---|---|
| `https://accanto.care/` | Vetrina Astro raggiungibile | 200 + body contiene `Accanto` | 5 min |
| `https://app.accanto.care/` | SPA Vite servita | 200 + body contiene `<div id="root">` | 5 min |
| `https://api.accanto.care/api/health/ready` | API + DB | 200 con `"status":"ok"`. 503 se DB giù | 1–5 min |

Note implementative su UptimeRobot:

- **Type**: HTTP(s) — non Keyword per `/health/ready` (basta lo
  status code; il body è già verificato dall'app stessa).
- **Monitor timeout**: 30 s (oltre = considera FAIL).
- **Alert contacts**: email primaria del team + (futuro)
  webhook → canale `#alerts`.
- **Alert when DOWN for**: 2 cicli consecutivi (evita falsi
  positivi su jitter di rete da datacenter UptimeRobot).
- **SSL monitoring**: ON (Better SSL su tutti, alert 14gg prima
  della scadenza del cert — anche se Caddy rinnova automaticamente
  via ACME, l'alert è un canary per "Caddy non sta più rinnovando").

`/health/ready` è il check giusto perché distingue "il processo
risponde" (sempre 200 anche durante crash-loop del DB) da "il
servizio può servire traffico" (200 sse il DB risponde). Vedi
[Program.cs](../../backend/src/Accanto.Api/Program.cs).

## Dead-man's switch (Healthchecks.io)

Idea: per ogni job pianificato creo un check su Healthchecks.io
con grace period sensato. Il job pinga l'endpoint **solo a fine
esecuzione riuscita**. Se non arriva ping entro periodo + grace,
Healthchecks.io alza alert.

### Check da creare

| Check | Schedule atteso | Grace | Cosa fa lo script |
|---|---|---|---|
| `accanto-backup-daily` | ogni 24h (cron alle 03:15) | 2h | `scripts/db/backup.ps1` pinga a fine dump cifrato + sha256. |
| `accanto-restore-drill-weekly` | ogni 7 giorni | 24h | `scripts/db/restore-drill.ps1` pinga solo se 13/13 check passano. |
| `accanto-cert-canary` | ogni 24h | 6h | (futuro) cron che verifica `openssl s_client` su `api.accanto.care:443` e controlla scadenza > 14gg. |
| `accanto-secret-rotation-drill` | ogni 365 giorni | 14 giorni | Manuale: chi esegue il drill annuale (gennaio) pinga alla fine. Grace lungo perché umano. |

### Configurazione lato job

Gli script Accanto leggono l'URL del check dall'env, opt-in:

```powershell
# Crontab (Linux server) o Task Scheduler (Windows server):
$env:HEARTBEAT_BACKUP_URL  = "https://hc-ping.com/<uuid-backup>"
$env:HEARTBEAT_RESTORE_URL = "https://hc-ping.com/<uuid-restore>"
$env:BACKUP_PASSPHRASE     = "<dal password manager>"
pwsh ./scripts/db/backup.ps1
```

Comportamento:

- [backup.ps1](../../scripts/db/backup.ps1): se
  `HEARTBEAT_BACKUP_URL` è impostato e il dump+cifratura+sha256
  sono OK, fa `POST` con body `size=... sha256=... file=...`.
  Errori di rete sul ping vengono **loggati ma non fanno fallire
  il backup** (il backup è già su disco): l'assenza di ping su
  Healthchecks scatena comunque l'alert lato esterno.
- [restore-drill.ps1](../../scripts/db/restore-drill.ps1): pinga
  `HEARTBEAT_RESTORE_URL` SOLO se `$exitCode -eq 0` (cioè 13/13
  PASS). Drill fallito → nessun ping → alert.

### Quando NON pingare

Importante: **non spostare il ping in `finally`**. La regola è "ping
sse il job ha fatto il suo dovere". Se il dump scrive su disco un
file da 0 byte ma il cifratore va in errore, _non_ vogliamo che
Healthchecks vada verde: l'assenza del backup utilizzabile è
proprio il fallimento che il dead-man's switch deve catturare.

### Self-hosted opzione

Se in futuro serve sovranità completa (cliente sanitario con
vincoli GDPR rigidi), Healthchecks è anche
[self-hostabile](https://github.com/healthchecks/healthchecks) (Docker,
PostgreSQL backend). Migrazione = stesso UUID-style URL via env,
zero modifiche agli script.

## Alert routing

Free tier OK per piccolo team:

- **Email**: tutti i monitor → alert@accanto.care (lista, non singola
  persona).
- **Telegram bot** (Healthchecks supporta nativo, gratis): canale
  `#accanto-alerts` privato, on-call gira manualmente settimanale.
- **Webhook futuro**: quando si introdurrà PagerDuty/Opsgenie,
  ridirezione webhook → quel sistema. Frattanto telegram + email
  bastano per un servizio non 24/7.

Severità (convenzione):

- **CRITICAL**: API/SPA/vetrina giù > 5 min, backup non eseguito
  > 26h, restore drill fallito.
- **WARNING**: SSL cert < 14gg, response time > 5s su `/health/ready`.
- **INFO**: deploy completati (push da CI, non da monitoring).

## Smoke test (post-setup)

Una volta configurati i monitor:

1. **Uptime UptimeRobot**: fermare il container backend
   (`docker compose stop backend`), attendere 2 cicli (~10 min),
   verificare che arrivi l'email di DOWN. Ripristinare e
   verificare email di UP.
2. **Dead-man's switch**: nel cron-job di backup, settare
   `HEARTBEAT_BACKUP_URL` a un check con interval 1h e grace 5
   min. Eseguire 1 volta, attendere conferma "up" su
   Healthchecks. Poi disabilitare il cron per 75 minuti,
   verificare che arrivi alert "down".
3. **Healthchecks self-resolve**: il check torna verde
   automaticamente al ping successivo, non serve acknowledge
   manuale.

## Cosa NON copre questo runbook

- **Application performance monitoring** (latency p95, throughput
  per endpoint): coperto da Seq via query Serilog (vedi
  [logging.md](logging.md)). Per APM dedicato in futuro:
  OpenTelemetry → Tempo/Jaeger.
- **Log alerting su pattern** (es. >10 login falliti per IP/min):
  coperto dalle query Seq in [logging.md](logging.md) §"Query di
  sicurezza", da promuovere a Seq Alert quando il volume di
  produzione lo giustifica.
- **Pager rotation / on-call schedule**: fuori scope per ora,
  team piccolo, mode best-effort.

## Storia

| Data | Cambio |
|---|---|
| 2026-06-04 | Runbook creato. Heartbeat opt-in in `backup.ps1` e `restore-drill.ps1`. Decision: Healthchecks.io per job interni + UptimeRobot per endpoint pubblici. |
