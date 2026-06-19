# Audit di sicurezza supply-chain

Documento di riferimento per gli scan di sicurezza eseguiti sul progetto
Accanto. L'obiettivo è fornire una baseline ripetibile per:

- vulnerabilità nelle immagini Docker pubblicate su GHCR;
- segreti accidentalmente committati nella history git;
- hardening delle immagini (CIS Docker Benchmark).

Tutti i tool girano in container, così non è necessario installare nulla
sull'host (Windows o Linux). I comandi sono in PowerShell ma equivalenti
ovvi su bash.

## Tool utilizzati

| Tool | Scopo | Immagine container |
|---|---|---|
| [Trivy](https://aquasecurity.github.io/trivy/) | Vulnerabilità OS + librerie applicative | `aquasec/trivy:latest` |
| [Gitleaks](https://github.com/gitleaks/gitleaks) | Segreti nella git history | `zricethezav/gitleaks:latest` |
| [Dockle](https://github.com/goodwithtech/dockle) | Hardening immagini (CIS) | `goodwithtech/dockle:latest` |
| [OWASP ZAP](https://www.zaproxy.org/) | DAST passivo (baseline) sui servizi HTTP | `ghcr.io/zaproxy/zaproxy:stable` |

Cache trivy persistente su volume named `trivy-cache` per evitare di
ri-scaricare il DB CVE a ogni run.

In CI gli stessi scan girano automaticamente via
[.github/workflows/security.yml](../.github/workflows/security.yml) su
PR, push su `main` e ogni lunedì mattina UTC. La build fallisce se
trivy trova vulnerabilità `HIGH`/`CRITICAL` con fix disponibile, o se
gitleaks trova segreti non presenti in [.gitleaks.toml](../.gitleaks.toml).

## Scope

Le 4 immagini in produzione del compose:

- `accanto-backend` — ASP.NET Core 10 su Ubuntu 24.04.
- `accanto-frontend` — SPA Vite servita da nginx 1.27 (Alpine).
- `accanto-web` — sito vetrina Astro servito da nginx 1.27 (Alpine).
- `caddy:2-alpine` — reverse proxy upstream, non buildato da noi.

Più la git history completa del repo.

## Comandi di riferimento

### 1. Vulnerabilità immagini (trivy)

Su Windows Docker Desktop il container trivy non vede il socket di
Docker, quindi le immagini vanno esportate prima con `docker save`:

```powershell
# Build/pull delle immagini target
docker compose build backend frontend web
docker pull caddy:2-alpine

# Esporta in tar
docker save accanto-backend:latest  -o accanto-backend.tar
docker save accanto-frontend:latest -o accanto-frontend.tar
docker save accanto-web:latest      -o accanto-web.tar
docker save caddy:2-alpine          -o caddy.tar

# Scan (HIGH + CRITICAL)
foreach ($tar in 'accanto-backend.tar','accanto-frontend.tar','accanto-web.tar','caddy.tar') {
    Write-Host "=== $tar ===" -ForegroundColor Cyan
    docker run --rm `
        -v "${PWD}:/work" `
        -v "trivy-cache:/root/.cache/" `
        aquasec/trivy:latest image `
        --severity HIGH,CRITICAL `
        --quiet `
        --input "/work/$tar"
}

Remove-Item *.tar
```

Su Linux/macOS è sufficiente:

```bash
docker run --rm \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v trivy-cache:/root/.cache/ \
  aquasec/trivy:latest image --severity HIGH,CRITICAL accanto-backend:latest
```

### 2. Segreti nella git history (gitleaks)

```powershell
docker run --rm -v "${PWD}:/repo" zricethezav/gitleaks:latest `
    detect --source /repo --report-format json --report-path /repo/gitleaks.json
```

Il file `gitleaks.json` contiene tutti i finding con file, riga, commit
e tipo di regola. Falsi positivi noti vanno documentati nella sezione
[Falsi positivi accettati](#falsi-positivi-accettati).

### 3. Hardening (dockle)

```powershell
foreach ($img in 'accanto-backend:latest','accanto-frontend:latest','accanto-web:latest') {
    Write-Host "=== $img ===" -ForegroundColor Cyan
    docker run --rm -v //var/run/docker.sock:/var/run/docker.sock `
        goodwithtech/dockle:latest $img
}
```

### 4. DAST baseline (ZAP)

Gira contro lo stack `docker compose` locale, sulla rete `accanto_default`,
così ZAP raggiunge i container per nome senza passare dalle porte
pubblicate sull'host (utile su Windows dove processi locali possono
occupare le stesse porte e oscurare i container).

```powershell
docker compose up -d --build
New-Item -ItemType Directory -Force -Path zap-reports | Out-Null

foreach ($t in @(
    @{ host = 'backend:8080';  name = 'backend' },
    @{ host = 'frontend:80';   name = 'frontend' },
    @{ host = 'web:80';        name = 'web' }
)) {
    Write-Host "=== $($t.name) ===" -ForegroundColor Cyan
    docker run --rm --network accanto_default `
        -v "${PWD}/zap-reports:/zap/wrk" -t `
        ghcr.io/zaproxy/zaproxy:stable `
        zap-baseline.py -t "http://$($t.host)" `
            -r "$($t.name).html" -J "$($t.name).json"
}
```

Report HTML + JSON finiscono in `zap-reports/` (cartella in `.gitignore`).

### 5. Probe IDOR / tenant isolation

Script PowerShell che registra due utenti (Alice/Bob), crea risorse per
Alice e prova ad accedervi come Bob su tutti gli endpoint scoped al
cerchio di cura (timeline, doctor questions, shared updates, documents,
invites, audit, AI, export PDF). Atteso: ogni tentativo respinto con
`401/403/404`.

```powershell
# Stack attivo su http://localhost:8080
powershell -NoProfile -File scripts/security/tenant-isolation-probe.ps1
```

Exit code `0` = nessuna violazione, `1` = almeno un endpoint ha
risposto `2xx` a una richiesta cross-tenant.

## Risultati ultimo run (v0.8.0 → patch v0.8.1)

Data: **2026-06-03**

### Trivy — vulnerabilità

| Immagine | Base OS | Findings v0.8.0 | Findings dopo patch |
|---|---|---|---|
| `accanto-backend` | ubuntu 24.04 + .NET 10.0.8 | **0 HIGH/CRITICAL** | 0 |
| `accanto-frontend` | nginx:1.27-alpine | 5 HIGH | **0** |
| `accanto-web` | nginx:1.27-alpine | 5 HIGH | **0** |
| `caddy:2-alpine` | alpine 3.23.4 | 1 HIGH (go-jose CVE-2026-34986) | upstream |

Dettaglio dei 5 HIGH ricorrenti sulle immagini nginx (tutti nei layer
Alpine di base, non in codice nostro):

- `libxml2` CVE-2025-49794, CVE-2025-49795, CVE-2025-49796, CVE-2026-6732
  → DoS, fix in `2.13.9-r1`.
- `musl` CVE-2026-40200 → RCE potenziale, fix in `1.2.5-r11`.
- `nghttp2-libs` CVE-2026-27135 → DoS, fix in `1.68.1`.
- `zlib` CVE-2026-22184 → RCE nell'utility `untgz` (non usata da nginx
  runtime), fix in `1.3.2-r0`.

**Remediation applicata**: `RUN apk upgrade --no-cache` subito dopo
`FROM nginx:1.27-alpine` in `frontend/Dockerfile` e `web/Dockerfile`.
Costo: zero a runtime; aumenta solo il tempo di build di ~2 secondi.

**Caddy**: il CVE su `go-jose v3.0.4` (DoS via JWE) è risolto in v3.0.5
upstream. Si pulisce automaticamente al prossimo refresh
dell'immagine `caddy:2-alpine`. Nessuna azione lato Accanto.

### Gitleaks — segreti

| Run | Commit scannati | Finding |
|---|---|---|
| 2026-06-03 | 68 | 1 (falso positivo, vedi sotto) |

### Dockle — hardening

- **backend**: solo INFO. USER `accanto` non-root, healthcheck nel
  compose, niente segreti. ✅
- **frontend / web**: il container ora gira su immagine
  `nginxinc/nginx-unprivileged:1.27-alpine` come utente `nginx`
  (UID 101) ed ascolta sulla porta non privilegiata 8080. Chiuso
  `CIS-DI-0001`. ✅
- 1 FATAL `CIS-DI-0010` (KEY_SHA512) su frontend/web: **falso positivo**
  (chiave GPG di verifica APK nei layer di base nginx, non un secret
  applicativo).

### ZAP — DAST baseline

Lanciato sullo stack locale dietro `accanto_default`. Sequenza prima e
dopo l'hardening dei `nginx.conf` (aggiunta header di sicurezza base via
`security-headers.conf` incluso in ogni `location`):

| Target | FAIL | WARN prima | WARN dopo | Note |
|---|---|---|---|---|
| `backend:8080` | 0 | 1 | 1 | `Storable and Cacheable Content` su `/health`. Accettato. |
| `frontend:80` | 0 | 8 | **4** | CSP/COEP forniti da Caddy in prod. |
| `web:80` | 0 | 7 | **3** | CSP/COEP forniti da Caddy in prod. |

Header aggiunti a nginx (defense-in-depth):
`X-Content-Type-Options nosniff`, `X-Frame-Options DENY`,
`Referrer-Policy strict-origin-when-cross-origin`,
`Permissions-Policy geolocation=(), microphone=(), camera=(), payment=(), usb=()`,
`server_tokens off`.

WARN residui dopo hardening (accettati):

- `Content Security Policy (CSP) Header Not Set` → fornita da Caddy in
  produzione (vedi [deploy/Caddyfile](../deploy/Caddyfile), blocco
  `security_headers` + CSP per dominio).
- `Cross-Origin-Embedder-Policy` → anch'esso fornito da Caddy (`COOP` /
  `CORP`). Non serve a nginx-standalone.
- `Storable but Non-Cacheable Content` → INFO su pagine HTML, voluto.
- `Modern Web Application` → INFO, non actionable.

### Probe IDOR / tenant isolation

21 probe cross-tenant su tutti gli endpoint `care-circles/{id}/...`
(circle, timeline, doctor-questions, shared-updates, documents,
invites, audit, AI settings/operations, export PDF).

| Data | Probe | PASS | FAIL | Note |
|---|---|---|---|---|
| 2026-06-03 | 21 | **21 (100%)** | 0 | Tutti gli endpoint rispondono `403 Forbidden`. |

Nessun IDOR. La pipeline di autorizzazione (`EnsureMemberAsync` su
`ICareCircleAuthorization`) viene invocata correttamente su ogni
resource scoped al cerchio prima di qualunque accesso al DB.

## Falsi positivi accettati

| Tool | Finding | File / contesto | Motivazione |
|---|---|---|---|
| gitleaks | `generic-api-key` valore `test-key-very-long-test-key-very-long-1234` | `backend/tests/Accanto.Tests/AccantoFactory.cs:18` | Chiave fittizia usata solo dai test di integrazione. Non concede alcun accesso. Esclusa via [.gitleaks.toml](../.gitleaks.toml). |
| dockle | `CIS-DI-0010` su `KEY_SHA512` ENV | immagini `accanto-frontend`, `accanto-web` | Variabile ereditata dal layer base nginx, usata per verificare le firme APK. Non è un secret applicativo. |

## Miglioramenti tracciati

1. Spostare nginx (frontend + web) su porta non privilegiata e attivare
   `USER nginx` per chiudere `CIS-DI-0001`. ✅ Fatto il 2026-06-03:
   passaggio a `nginxinc/nginx-unprivileged:1.27-alpine`, listen 8080,
   port mapping `5173:8080`/`4321:8080`, `deploy/Caddyfile` aggiornato
   (`frontend:8080`, `web:8080`).
2. Aggiungere uno scan ZAP baseline contro lo stack `docker compose`
   locale, autenticato su 2 tenant di prova, per coprire IDOR / authz
   sui cerchi di cura. Più alto ROI applicativo del solo CVE scan.
   ✅ Baseline passiva eseguita il 2026-06-03; probe IDOR ad-hoc
   ([scripts/security/tenant-isolation-probe.ps1](../scripts/security/tenant-isolation-probe.ps1))
   eseguito stesso giorno con 21/21 PASS.
3. Wiring degli scan trivy + gitleaks in GitHub Actions
   (`.github/workflows/security.yml`) per failo automatico su PR e tag. ✅ Attivo dal 2026-06-03.
4. Passaggio del backend a immagine .NET *chiseled* (distroless) per
   eliminare shell e package manager dal runtime.
   ✅ Fatto il 2026-06-03: `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`,
   utente `app` (UID 1654), nessun apt/curl/wget, healthcheck container
   rimosso dal compose dev (probe esterno su `/health`).
5. Aggiornamento automatico delle dipendenze. ✅ Fatto il 2026-06-03:
   `.github/dependabot.yml` con scheduling settimanale per NuGet (backend),
   npm (frontend, web), Docker e GitHub Actions. PR raggruppate per
   ridurre il rumore.
6. SAST con CodeQL. ✅ Fatto il 2026-06-03:
   `.github/workflows/codeql.yml` analizza `csharp` (build manuale) e
   `javascript-typescript` (build-mode none) su push/PR/cron settimanale
   con query suite `security-and-quality`.
7. Hardening container runtime. ✅ Fatto il 2026-06-03 su backend,
   frontend e web in [docker-compose.yml](../docker-compose.yml):
   `security_opt: no-new-privileges`, `cap_drop: ALL`, `read_only: true`
   rootfs con `tmpfs` su `/tmp`, `/home/app/.aspnet` (DataProtection),
   `/var/cache/nginx`. Caddy in prod stessa cosa + `cap_add: NET_BIND_SERVICE`
   per le porte 80/443.
8. Audit Caddyfile. ✅ Fatto il 2026-06-03: protocolli espliciti
   (h1/h2/h3) sul listener `:443`, `request_body { max_size 25MB }` sul
   reverse proxy API (allineato a `Storage__MaxFileSizeBytes`), formattazione
   passata da `caddy fmt`.
9. `security.txt` RFC 9116 + nginx route per `/.well-known/`.
   ✅ Fatto il 2026-06-04: [web/public/.well-known/security.txt](../web/public/.well-known/security.txt)
   pubblicato sotto `https://accanto.care/.well-known/security.txt`
   con `Contact` (GitHub Security Advisories + mailto), `Expires`,
   `Canonical`, `Policy` → `SECURITY.md`. In nginx (web) location
   esplicita `^~ /.well-known/` con `Cache-Control: no-cache` prima
   del fallback SPA, così il file viene servito col MIME corretto.
10. Content Security Policy + COOP/CORP gestiti anche da nginx
    (defense-in-depth oltre a Caddy). ✅ Fatto il 2026-06-04:
    [frontend/security-headers.conf](../frontend/security-headers.conf) e
    [web/security-headers.conf](../web/security-headers.conf) includono
    `Content-Security-Policy`, `Cross-Origin-Opener-Policy: same-origin`,
    `Cross-Origin-Resource-Policy: same-site`. CSP frontend stretta
    (`default-src 'self'`, no inline JS, `frame-ancestors 'none'`,
    `object-src 'none'`); CSP web allow-list Google Fonts solo dove serve.
11. Probe RBAC autenticata sui ruoli `Owner`/`Caregiver`/`Viewer` di un
    cerchio di cura. ✅ Fatto il 2026-06-04, 23/23 PASS.
    ([scripts/security/rbac-probe.ps1](../scripts/security/rbac-probe.ps1))
    Copre: lettura cerchio/timeline/doctor-q/shared-upd/documents/audit
    per `Viewer` (allow su read, deny su mutazioni), `Caregiver` (allow
    su mutazioni operative, deny su inviti/AI settings/eliminazione
    cerchio).
12. Probe rate-limit + lockout sulle policy
    `auth-login`/`auth-register`/`auth-sensitive`/`invite-create`.
    ✅ Fatto il 2026-06-04, 4/4 policy PASS.
    ([scripts/security/rate-limit-probe.ps1](../scripts/security/rate-limit-probe.ps1))
    Lo script ricarica temporaneamente il backend con limiti bassi via
    override compose (`RateLimit__*__PermitLimit=3`), spara N+1
    richieste per ciascuna policy e verifica che la `(N+1)`-esima
    riceva `429`. A fine probe ripristina i default di Development.
13. Hardening JWT: whitelist algoritmi `HS256`, `RequireSignedTokens=true`,
    `RequireExpirationTime=true`, fail-fast all'avvio se la chiave è
    minore di 32 caratteri. Applicato sia al bearer principale sia al
    challenge 2FA (`JwtTokenService.ValidateTwoFactorChallenge`). ✅
    Fatto il 2026-06-04. Mitiga downgrade ad `alg=none`/`HS256` con
    chiave debole e accetta solo token correttamente firmati e scaduti.
14. Separazione ruoli Postgres: `accanto` (owner/migrator, DDL) vs
    `accanto_app` (runtime, solo DML). Lo script
    [scripts/db/init/01-app-role.sh](../scripts/db/init/01-app-role.sh)
    crea/aggiorna il ruolo applicativo al primo boot del volume
    (`docker-entrypoint-initdb.d`), assegna `SELECT/INSERT/UPDATE/DELETE`
    sulle tabelle correnti + `USAGE/SELECT/UPDATE` sulle sequenze,
    aggiunge `ALTER DEFAULT PRIVILEGES FOR ROLE accanto` per le tabelle
    create dalle migration future, e fa `REVOKE CREATE ON SCHEMA public`
    a `accanto_app`/`PUBLIC` per impedire `CREATE TABLE` runtime.
    Il backend usa ora due connection string distinte: `Postgres`
    (runtime, `accanto_app`) e `PostgresMigrator` (`accanto`, applicata
    solo al `MigrateAsync()` all'avvio e dalla CLI). ✅ Fatto il
    2026-06-04. Verificato manualmente che `CREATE TABLE evil` come
    `accanto_app` fallisce con `permission denied for schema public`.
    Regressione: probe tenant isolation 21/21 PASS, probe RBAC 23/23
    PASS, suite test 131/131 PASS.
15. Hardening upload documenti: magic-bytes sniffing
    (`FileSignatureValidator`) sui primi 16 byte per PDF/PNG/JPEG e per
    `text/plain` (rifiuto di byte di controllo non printabili),
    integrato in `DocumentService.UploadAsync` dopo il check
    content-type allow-list. Scan antivirus opzionale via interfaccia
    `IMalwareScanner`: `NoopMalwareScanner` di default,
    `ClamAvMalwareScanner` (protocollo `INSTREAM` TCP) attivabile
    impostando `ClamAV:Host` (es. `clamav` con il profilo
    `docker compose --profile av up -d`). Probe end-to-end
    [scripts/security/upload-probe.ps1](../scripts/security/upload-probe.ps1)
    verifica 5 casi (spoof PNG-as-PDF, content-type fuori allow-list,
    spoof PNG-as-text, happy PDF, happy text). ✅ Fatto il 2026-06-04,
    5/5 PASS. Suite test 131/131 PASS (13 nuovi unit test).
16. ZAP full scan autenticato in CI:
    [.github/workflows/zap-full-auth.yml](../.github/workflows/zap-full-auth.yml)
    builda lo stack `docker compose`, registra un utente di test via API
    e cattura il bearer JWT, lo inietta come header `Authorization` su
    tutte le richieste outbound di ZAP tramite "replacer rule"
    (`zap-work/zap.conf`), quindi lancia `zap-full-scan.py` (active +
    passive) contro `http://localhost:8080`. Schedulato il lunedì
    mattina UTC + `workflow_dispatch`. Fallisce su alert FAIL (HIGH);
    WARN/INFO sono silenziati (`-I`). Il report HTML/JSON viene caricato
    come artifact `zap-report` (retention 30 giorni). ✅ Fatto il
    2026-06-04. Sostituisce il giro `zap-baseline.py` manuale per i casi
    in cui serve coverage attiva (es. pre-release).

17. **Tabelle di audit append-only a livello DB** ✅ Fatto il 2026-06-04.
    Dopo `MigrateAsync()`, in [backend/src/Accanto.Api/Program.cs](../backend/src/Accanto.Api/Program.cs)
    si esegue `REVOKE UPDATE, DELETE ON security_audit_log_entries,
    audit_log_entries FROM accanto_app` (idempotente, no-op se il
    ruolo non esiste). Il ruolo runtime conserva `SELECT` (lettura via
    `AuditController`) e `INSERT` (scrittura eventi via
    `SecurityAuditLog`) ma non può più mutare o cancellare righe
    storiche. Difesa in profondità: anche con SQLi o RCE applicativa
    l'attaccante non può ripulire le proprie tracce. Verifica manuale:
    `psql -U accanto_app -c 'DELETE FROM audit_log_entries WHERE 1=0'`
    → `ERROR: permission denied for table audit_log_entries`.
18. **Ordine middleware Serilog/ErrorHandling corretto** ✅ Fatto il
    2026-06-04. `app.UseSerilogRequestLogging()` precede ora
    `app.UseMiddleware<ErrorHandlingMiddleware>()`: Serilog vede la
    risposta finale (`403` per `ForbiddenException`) invece
    dell'eccezione non gestita (loggata come `500`). Riduce il rumore
    nei log/alert senza cambiare la risposta HTTP al client.
19. **Backup cifrato + restore drill testato** ✅ Fatto il 2026-06-04.
    [scripts/db/backup.ps1](../scripts/db/backup.ps1) esegue `pg_dump -Fc`
    + `openssl enc -aes-256-cbc -pbkdf2 -iter 600000` via container
    `alpine/openssl` (zero dipendenze host); output
    `backups/accanto-YYYYMMDD-HHMMSS.dump.enc` + sidecar `.sha256`.
    [scripts/db/restore-drill.ps1](../scripts/db/restore-drill.ps1)
    decifra, ripristina su Postgres effimero (tmpfs, porta 55432,
    isolato dal DB di lavoro) ed esegue 13 sanity check (esistenza
    tabelle critiche, row count, FK orfani, migration history
    leggibile). Primo drill: 13/13 PASS in ~25 s. Runbook completo con
    procedura DR step-by-step, RPO 24h / RTO 1h, retention
    7d+4w+12m+7y, schedule drill mensile in
    `accanto-ops/backup-restore.md` (repo separato).
20. **Secret rotation runbook** ✅ Fatto il 2026-06-04.
    `accanto-ops/secret-rotation.md` (repo separato)
    inventaria tutti i segreti (Postgres owner/app, `Jwt__Key`,
    `Encryption__MasterKey`, `BACKUP_PASSPHRASE`, cloud keys) con
    blast radius, cadenza e procedura per-segreto. Sezione "compromise
    scenario" per rotazione di emergenza in ordine di impatto. Drill
    annuale calendarizzato (primo lunedi di gennaio). La rotazione di
    `Encryption__MasterKey` e' zero-downtime grazie al supporto
    multi-chiave gia' presente (`KeyRotationService` CLI). `Jwt__Key`
    ora supporta anch'esso multi-key via `IssuerSigningKeyResolver`
    + claim `kid` nell'header (vedi item 23) → rotazione zero-downtime
    anche per JWT.
21. **Rate-limit per-IP a livello edge (Caddy)** ✅ Fatto il 2026-06-04.
    [deploy/caddy/Dockerfile](../deploy/caddy/Dockerfile) builda Caddy
    con il modulo `github.com/mholt/caddy-ratelimit` via `xcaddy`.
    [deploy/Caddyfile](../deploy/Caddyfile) aggiunge tre zone sliding
    window per-IP: `/auth/login` 30/min, `/auth/refresh` 60/min,
    `/auth/register` 5/h. Defense-in-depth complementare al rate-limit
    applicativo per-utente (che non scatta su credential stuffing
    distribuito su molti username dalla stessa macchina). Validato
    funzionalmente: 5 register PASS → 6°+7° → `429 Too Many Requests`.
    Caddyfile validato con `caddy validate`.
22. **Dependency scanning .NET + npm in CI** ✅ Fatto il 2026-06-04.
    [.github/workflows/security.yml](../.github/workflows/security.yml) ha
    due nuovi job: `dotnet-deps` (esegue `dotnet list package --vulnerable
    --include-transitive`, fallisce su High/Critical) e `npm-audit` (matrix
    su `frontend` + `web`, usa [scripts/ci/npm-audit-check.sh](../scripts/ci/npm-audit-check.sh)
    che fallisce su High/Critical non in allowlist). Allowlist tracciate
    per-progetto in `<project>/.npm-audit-allow` con motivo + scadenza per
    ogni eccezione. Stato attuale: backend 0 vuln, frontend solo moderate,
    web 1 high tollerata (GHSA-wrwg-2hg8-v723 Astro server-islands XSS
    non esploitabile in build statico, upgrade a Astro 5.x schedulato
    entro 2026-09-01). Complementa Trivy (che scansiona il layer OS+lib
    delle immagini docker) con la scansione delle dipendenze sorgente.
23. **JWT multi-key con `IssuerSigningKeyResolver` (rotazione zero-downtime)** ✅ Fatto il 2026-06-04.
    [JwtOptions.cs](../backend/src/Accanto.Infrastructure/Security/JwtOptions.cs)
    introduce schema multi-key `Jwt__Keys__<keyId>=<base64>` + `Jwt__ActiveKeyId`
    con fail-fast all'avvio (chiavi <32 char, `ActiveKeyId` mancante o
    non presente in `Keys`). Backward compat: il vecchio `Jwt__Key` viene
    promosso al `kid` `"legacy"` e continua a funzionare.
    [JwtTokenService.cs](../backend/src/Accanto.Infrastructure/Security/JwtTokenService.cs)
    firma con `SymmetricSecurityKey.KeyId = ActiveKeyId` → il JWT emesso
    porta `kid` nell'header. La validazione in
    [Program.cs](../backend/src/Accanto.Api/Program.cs) usa
    `IssuerSigningKeyResolver = (_, _, kid, _) => jwtSigning.Resolve(kid)`,
    mappando il `kid` del token alla chiave corretta; token vecchi senza
    `kid` vengono provati con TUTTE le chiavi → grace period semplice
    (basta tenere la vecchia chiave nel dict finché tutti gli access
    token in circolazione scadono). Coperto da 10 test unit
    (`JwtSigningMaterialTests`: parsing config, fail-fast, propagazione
    `kid`, validazione cross-key, rifiuto dopo rimozione, validazione di
    token legacy senza kid). Runbook secret-rotation §3 aggiornato con
    procedura zero-downtime + procedura di emergenza con revoca refresh.
24. **Forensic snapshot pre-incident + offsite S3** ✅ Fatto il 2026-06-04.
    [scripts/security/forensic-snapshot.ps1](../scripts/security/forensic-snapshot.ps1)
    cattura in un singolo bundle `.forensic.tar.gz` (+ `.sha256` per
    chain of custody) lo stato del sistema PRIMA di iniziare la
    risposta a un incidente: dump DB cifrato, audit_log + security_audit
    in CSV (ultimi 30 gg), refresh_token attivi, users summary,
    `docker inspect` di tutti i container, lista immagini con digest,
    log container ultime 72h, sha256 di `.env`, manifest.json con
    git rev + operator + sha256 per file. Risolve il problema classico
    del "ho ruotato i segreti e restartato → ho distrutto le prove
    volatili". Lo step 0 del compromise scenario in
    `accanto-ops/secret-rotation.md` (repo separato)
    e' ora "lancia forensic-snapshot.ps1 PRIMA di toccare qualunque cosa".
    [scripts/db/backup-offsite.ps1](../scripts/db/backup-offsite.ps1)
    + [.env.backup-offsite.example](../.env.backup-offsite.example)
    forniscono il wrapper per upload incrementale idempotente su S3-
    compatible (IONOS, AWS, Backblaze) via `amazon/aws-cli` in docker.
    Policy bucket consigliata: `s3:Put/Get/List` only (NO `Delete`),
    object-lock + versioning + lifecycle (retention 7 anni). Le
    credenziali IONOS reali andranno in `.env.backup-offsite` (in
    `.gitignore`) appena il piano cloud sara' attivo: lo schema +
    placeholder + runbook backup-restore §2-3 sono gia' pronti.
25. **CSP reporting endpoint (raccolta violazioni)** ✅ Fatto il 2026-06-04.
    [SecurityReportsController.cs](../backend/src/Accanto.Api/Controllers/SecurityReportsController.cs)
    espone `POST /api/security/csp-report` (anonimo, rate-limit 100/min
    per-IP via policy `csp-report`, body cap 8 KB). Accetta entrambi i
    formati del browser: legacy `application/csp-report`
    (`{"csp-report":{...}}`) e moderno Reporting API
    `application/reports+json` (`[{type:"csp-violation",body:{...}}]`).
    I campi diagnostici (`violated-directive`, `blocked-uri`,
    `document-uri`, `source-file:line:col`, `disposition`, IP, UA)
    vengono normalizzati e emessi come log strutturati con categoria
    `Accanto.Security.Csp` (livello Information) → consumabili da Seq /
    Loki per dashboard e alert su pattern anomali. NON scritti su DB
    per evitare DoS via flood. [deploy/Caddyfile](../deploy/Caddyfile)
    aggiunge `report-uri` + `report-to csp-endpoint` alle due CSP (sito
    vetrina + SPA) e l'header `Reporting-Endpoints`. Risposta sempre
    `204 No Content` (anche su body invalido o vuoto: niente segnali
    utili a chi sonda l'endpoint). Coperto da 5 test integrazione
    (`CspReportEndpointTests`). Le CSP erano gia' in enforce dal day-1;
    questa e' la parte mancante per chiudere il loop di osservabilita'.
26. **SBOM CycloneDX in CI + allegata alle release** ✅ Fatto il 2026-06-04.
    Nuovo job `sbom` in [.github/workflows/security.yml](../.github/workflows/security.yml)
    genera ad ogni push/PR/schedulazione 3 SBOM CycloneDX 1.5 JSON:
    `backend.cdx.json` (via tool .NET `CycloneDX`, sul progetto runtime
    `Accanto.Api` con `-t` per transitive → esclude le deps di test che
    non finiscono in container), `frontend.cdx.json` e `web.cdx.json`
    (via `@cyclonedx/cyclonedx-npm`). Upload come workflow artifact
    `sbom-cyclonedx` (retention 90 gg). Il job stampa anche il numero
    di componenti per file come summary.
    [.github/workflows/release.yml](../.github/workflows/release.yml)
    rigenera le stesse SBOM al tag `vX.Y.Z` e le **allega come asset
    della GitHub Release** (versionamento immutabile, retention infinita)
    con sidecar `.sha256` per chain of custody.
    Valore vs dotnet-deps/npm-audit (che FALLISCONO la build su
    vuln High/Critical): la SBOM e' un INVENTARIO post-build, riusabile
    per (a) re-scan offline quando esce una nuova CVE senza ri-buildare,
    (b) audit di compliance richiesti da clienti enterprise/sanitario
    (allineato a NTIA Minimum Elements + Executive Order 14028), (c)
    diff delle dipendenze tra release per change management.
    Consumabile da Dependency-Track, Grype, OSV-Scanner, Snyk.
27. **SLSA build provenance firmata su immagini GHCR** ✅ Fatto il 2026-06-04.
    [.github/workflows/release.yml](../.github/workflows/release.yml)
    aggiunge due livelli di attestation a ogni immagine pushata:
    (a) **BuildKit-native** (`provenance: mode=max` + `sbom: true` su
    `docker/build-push-action`) → SLSA provenance + SPDX SBOM embedded
    nell'OCI manifest come referrers, inclusi Dockerfile e build args
    nei materials;
    (b) **GitHub-signed via Sigstore keyless** (`actions/attest-build-provenance@v2`
    con `push-to-registry: true`) → SLSA v1.0 statement firmato con
    certificato ephemeral Fulcio e trasparenza Rekor, pubblicato sia
    nella tab Security > Attestations del repo sia come referrer OCI.
    Permessi aggiunti al workflow: `id-token: write` (OIDC per Fulcio) +
    `attestations: write` (pubblicazione su GitHub).
    Verifica da consumer:
    ```sh
    gh attestation verify oci://ghcr.io/andreaprestia/accanto-backend:v0.8.2 \
      --repo AndreaPrestia/Accanto
    ```
    Conferma criptograficamente che l'immagine e' stata buildata da
    questa repo, in questo workflow `release.yml`, a partire da quel
    commit specifico (mitigazione di tipo-squatting, dependency
    confusion, e supply-chain attack su immagini "look-alike" pushate
    su altri registry). Allineato a SLSA build level 3 (build platform
    isolata = GitHub Actions runner, provenance non falsificabile,
    OIDC + Fulcio + Rekor). Prossimi step opzionali per arrivare a
    L3 pieno: hosted build platform → policy-gated deployment.
28. **Log centralizzato (Seq) + enricher di contesto request** ✅ Fatto il 2026-06-04.
    Stack Seq via profilo `observability` di
    [docker-compose.yml](../docker-compose.yml) era gia' presente ma il
    backend non lo riceveva di default e nessun log di applicazione
    portava `UserId`/`ClientIp`/`RequestId` → filtering inutile in UI.
    Aggiunto [`LogContextEnrichmentMiddleware`](../backend/src/Accanto.Api/Common/LogContextEnrichmentMiddleware.cs)
    registrato dopo `UseAuthentication()`: pusha `RequestId`, `ClientIp`
    (X-Forwarded-For first hop, fallback peer, cap 45 char per evitare
    log injection da header arbitrari) e `UserId` (claim `sub` /
    `NameIdentifier`, null se anonimo) nel `Serilog.Context.LogContext`
    per tutta la durata della request. `UseSerilogRequestLogging` ha
    ora un `EnrichDiagnosticContext` che mette `ClientIp` + `UserAgent`
    anche sulla summary line.
    Backend in compose riceve `Logging__SeqUrl` / `Logging__SeqApiKey`
    come variabili opt-in (default vuoto = no-op, app logga solo su
    stdout). Per attivare basta `docker compose --profile observability
    up -d` + valorizzare `Logging__SeqUrl=http://seq:5341` in `.env`.
    Nuovo runbook `accanto-ops/logging.md` (repo separato):
    architettura, query Seq utili per scenari di sicurezza (login
    falliti per IP, rate-limit triggered, attivita' per utente, CSP
    violations, slow request, errori EF), retention raccomandata,
    backup volume `seq-data`, esposizione in produzione (VPN-only /
    basic-auth via Caddy / OIDC), smoke test.
    Effetto sicurezza: incident response passa da `grep` su
    `docker logs` a query indicizzate su tutto il fleet con join per
    `UserId`/`ClientIp` → forensica veloce, dashboard real-time, alert
    su pattern sospetti (cardine per i prossimi item su monitoring +
    2FA admin enforcement).
29. **Upload hardening profondo: estensione, magics pericolosi, struttura per-formato** ✅ Fatto il 2026-06-04.
    [`FileSignatureValidator`](../backend/src/Accanto.Application/Documents/FileSignatureValidator.cs)
    era limitato a 4 magic-byte sniff sui primi 16 byte: passava
    qualunque file iniziasse con la signature giusta, anche se il
    resto del contenuto era un eseguibile o un archivio (es. PDF
    polyglot, JPEG con coda ZIP, file con estensione `.pdf` ma
    Content-Type `application/pdf` veicolante un PE). Aggiunto metodo
    `Validate(content, contentType, fileName)` chiamato da
    [`DocumentService`](../backend/src/Accanto.Application/Documents/DocumentService.cs)
    sull'intero buffer (gia' materializzato per lo scan AV), che
    applica tre livelli di difesa:
    1. **Coerenza estensione ↔ content-type**: tabella allow-list per
       tipo (`pdf` → `.pdf`, `jpeg` → `.jpg`/`.jpeg`, `png` → `.png`,
       `text/plain` → `.txt`/`.log`/`.md`); mismatch ⇒ reject.
    2. **Rifiuto universale di magic eseguibili/archivio** a
       prescindere dal tipo dichiarato: PE/MZ (`4D 5A`), gzip
       (`1F 8B`), ELF (`7F 45 4C 46`), ZIP (`50 4B 03 04`), Mach-O
       (`FE ED FA CE/CF`, `CA FE BA BE`), RAR (`Rar!`), 7z
       (`37 7A BC AF 27 1C`).
    3. **Validazione strutturale per formato**: PDF deve avere header
       `%PDF-` + versione `1.x`/`2.x` + marker `%%EOF` negli ultimi 1024
       byte; PNG deve avere la signature di 8 byte seguita dal chunk
       `IHDR` come primo chunk (lunghezza + tipo a offset 8–16); JPEG
       deve terminare con marker EOI `FF D9` negli ultimi 16 byte;
       `text/plain` deve essere UTF-8 valido senza fallback (decoder
       in strict mode) e privo di byte di controllo (NUL, C0/C1 a
       parte tab/LF/CR, DEL).
    `IsValid(span, contentType)` resta per back-compat (head-only,
    nessuna validazione strutturale). Errori restituiti come messaggi
    italiani inglobati in `AppValidationException` → HTTP 422 con
    diagnostica utile lato client ("il PDF non termina con marker
    `%%EOF`", "estensione `.png` incoerente con `application/pdf`",
    "il contenuto contiene magic eseguibile (rifiutato a prescindere
    dal tipo dichiarato)", ecc.). 16 nuovi unit test
    (`FileSignatureValidatorTests`: PDF/PNG/JPEG well-formed e malformed,
    PE/ELF/ZIP rejection a prescindere dal tipo, UTF-8 invalido, NUL
    byte, unicode emoji valido, mismatch estensione); fixture esistenti
    in `DocumentServiceUploadGuardsTests` aggiornate per usare PDF
    strutturalmente validi (`%%EOF`). Probe end-to-end
    [scripts/security/upload-probe.ps1](../scripts/security/upload-probe.ps1)
    estesa con 2 nuove probe (PDF + estensione `.png` ⇒ 422; PE come
    `text/plain` ⇒ 422); totale 7 probe. Test 161/161 PASS.
    Effetto sicurezza: chiude la classe di attacchi "polyglot upload"
    (file che e' contemporaneamente un PDF e un ZIP/eseguibile a
    seconda di chi lo apre) e "extension confusion" (es. browser che
    sniffano l'estensione e ignorano `Content-Type`). Difesa cumulativa
    con magic-bytes head-only + AV (item 15) + extension whitelist
    nello storage.

30. **Monitoring esterno + dead-man's switch sui job pianificati** ✅ Fatto il 2026-06-04.
    `/health/ready` (200/503 con check DB, vedi
    [Program.cs](../backend/src/Accanto.Api/Program.cs)) e gli
    endpoint pubblici di vetrina/SPA esistevano gia' ma erano
    monitorati solo internamente (Serilog → Seq). Problema noto:
    se il backend crash-loopa o l'host e' giu', _l'unico processo
    che dovrebbe alertare_ e' anche quello morto → blind spot.
    Aggiunto runbook `accanto-ops/monitoring.md` (repo separato):
    Healthchecks.io (free, EU, self-hostabile) per i job interni +
    UptimeRobot (free) per gli endpoint pubblici. Decisione di usare
    due provider indipendenti cosi' un loro outage non azzera la
    visibilita'. Per ogni monitor: URL, intervallo, alert routing
    (email + Telegram), severita', smoke test post-setup.
    Implementato il **dead-man's switch** sui due cron critici:
    [scripts/db/backup.ps1](../scripts/db/backup.ps1) e
    [scripts/db/restore-drill.ps1](../scripts/db/restore-drill.ps1)
    leggono `HEARTBEAT_BACKUP_URL` / `HEARTBEAT_RESTORE_URL`
    dall'env (opt-in, nessuna dipendenza nuova) e fanno `POST` solo
    quando il job ha completato con successo (dump cifrato + sha256
    OK per il backup; 13/13 check PASS per il drill). Errore di
    rete sul ping viene loggato come warning ma non fa fallire il
    job (il backup e' gia' su disco); l'assenza di ping triggera
    comunque l'alert lato Healthchecks dopo la grace window. Il
    runbook documenta esplicitamente l'antipattern "ping in
    `finally`" (mascherebbe i fallimenti che il dead-man's switch
    deve catturare).
    Effetto sicurezza: chiude la classe di incident
    "cron silenziosamente fallito" — backup notturno che dal
    2026-04-15 non parte piu' perche' la passphrase e' stata
    rimossa dall'env, e nessuno se ne accorge fino al disastro.
    Sommato a item 19 (backup cifrato) + item 24 (forensic snapshot
    + offsite) la pipeline DR e' ora end-to-end monitorata.

31. **2FA obbligatorio per Owner con grace 7 giorni** ✅ Fatto il 2026-06-04.
    Gli Owner di un care circle hanno accesso a operazioni
    distruttive (delete circle, change role, export GDPR completo)
    e ai dati di tutti i membri: e' il ruolo piu' privilegiato.
    Finora 2FA era facoltativa; un Owner con password debole o
    leakata era game-over per il cerchio. Aggiunto enforcement:
    - Entita' `User.TwoFactorRequiredFromUtc` (timestamptz nullable)
      con migration di backfill SQL: ogni Owner pre-rollout
      riceve deadline = `NOW() + 7 giorni`. Cosi' nessun account
      esistente viene bloccato istantaneamente al deploy.
    - Middleware
      [`RequireTwoFactorForOwnersMiddleware`](../backend/src/Accanto.Api/Middleware/RequireTwoFactorForOwnersMiddleware.cs)
      tra `UseAuthorization` e `UseRateLimiter`: per ogni request
      autenticata controlla in una sola query
      `(TwoFactorEnabled, TwoFactorRequiredFromUtc, IsOwner)`. Se
      Owner senza 2FA e deadline scaduta → 403
      `application/problem+json` con `code: two_factor_required_for_owner`.
      Entro la grace, passa ma aggiunge header `X-2FA-Required-By`
      (ISO 8601) per il banner countdown frontend.
    - Whitelist minima volutamente: `/api/account/2fa/*`,
      `/api/account/me`, `/api/auth/{login,logout,refresh,2fa-login}`,
      `/api/security/csp-report`, `/swagger`, `/health`. Senza
      questa whitelist l'Owner scaduto non potrebbe MAI raggiungere
      `/api/account/2fa/setup` per uscire dal blocco → deadlock.
    - Lazy backfill: utente Owner con deadline=null (es. seed di

32. **Dual-write documenti su S3-compatible (IONOS) via outbox** ✅ Fatto il 2026-06-19.
    I documenti medici erano persistiti solo su volume locale
    (`/data/storage`, cifrato a riposo via `IFieldProtector`). Single
    point of failure: disk crash dell'host = perdita totale dei blob,
    indipendentemente dal backup logico del DB. Aggiunta replica
    sincrona-logica/asincrona-fisica su bucket S3 compatibile
    (`accanto-backups` IONOS, prefix `storage/`, **senza Object Lock**
    per restare compatibili con la cancellazione GDPR — il prefix
    `backups/*` invece resta Object Lock GOVERNANCE 7y).
    - Tabella nuova `document_sync_outbox` (`Operation` PUT/DELETE,
      `Status` pending/in_progress/done/failed, `RetryCount`,
      `NextAttemptAt`) con indice composito `(Status,NextAttemptAt)`.
      Migration `AddDocumentSyncOutbox`.
    - [`DocumentService.UploadAsync`](../backend/src/Accanto.Application/Documents/DocumentService.cs)
      e `DeleteAsync` inseriscono la riga outbox nella **stessa
      transazione** del SaveChangesAsync che persiste/cancella
      `medical_documents`: se il commit fallisce, DB e replica
      restano allineati (niente blob orfani su S3 senza riga nel DB).
    - [`DocumentSyncWorker`](../backend/src/Accanto.Infrastructure/Storage/DocumentSyncWorker.cs)
      `BackgroundService` poll-based (default 10s, batch 10). Risolve
      con scope DbContext fresco per ciclo via `IServiceScopeFactory`
      (evita il bug noto fire-and-forget + scoped DbContext →
      `Npgsql.NpgsqlOperationInProgressException`). Backoff
      esponenziale: 60s, 5min, 30min, 2h, 6h; oltre `MaxRetries=5`
      la riga finisce in `failed` per intervento manuale (alert da
      aggiungere quando il volume cresce).
    - [`S3DocumentReplica`](../backend/src/Accanto.Infrastructure/Storage/S3DocumentReplica.cs):
      `PutAsync` carica il blob **gia' cifrato** (no decifratura
      lato S3) con la stessa storage path; `DeleteAllVersionsAsync`
      pagina `ListVersionsRequest` e cancella ogni `VersionId` con
      `DeleteObjectRequest` — necessario perche' il bucket e'
      versionato e una semplice `DeleteObject` lascia la versione
      originale recuperabile (gap GDPR latente).
    - Gating completo via `S3DocumentReplica:Enabled` in
      `appsettings.json`: a `false` (default) nemmeno l'`IAmazonS3`
      viene istanziato in DI → zero dipendenze esterne in dev/test
      e build CI. Production override via env
      `S3DocumentReplica__*` (segreti AWS_ACCESS_KEY_ID/SECRET in
      `.env` non committato).
    - Test: 2 nuovi `DocumentSyncOutboxTests` (upload enqueue PUT;
      delete enqueue DELETE preservando PUT). Verifica dell'invariante
      "outbox e SaveChanges atomic" via DbContext in-memory.
    Effetto sicurezza: doppia copia geografica del dato cifrato. RPO
    della replica ≈ poll interval (10s) + worker latency. La chiave
    di cifratura resta solo lato application server → un attaccante
    che ruba il bucket S3 non puo' decifrare nulla (defense-in-depth
    rispetto a `IFieldProtector` + `Encryption__MasterKey`).

33. **GDPR right-to-erasure tombstone + cascade S3** ✅ Fatto il 2026-06-19.
    Il vecchio `AccountService.DeleteAsync` (a) faceva hard-delete
    dell'utente (impossibile soddisfare richieste di forense
    successive) e (b) **rifiutava** la cancellazione se l'utente
    partecipava a cerchi condivisi (incompatibile con il diritto
    all'oblio: GDPR art. 17 non ammette il blocco "ti cancello solo
    se nessun altro dipende dai tuoi dati"). Riscritto end-to-end
    in modalita' tombstone.
    - Nuovi campi `User.IsErased`, `ErasedAt`, `ErasureReason`
      (migration `AddUserErasure`). Email sostituita con
      `erased-{shortId}@accanto.invalid` (univoca, non recuperabile,
      non risolvibile in DNS); `DisplayName`="Utente cancellato";
      `PasswordHash` vuota (Verify() fallisce sempre); 2FA segreti
      azzerati.
    - [`UserErasureService`](../backend/src/Accanto.Application/Account/UserErasureService.cs):
      per ogni documento `UploadedByUserId == userId` inserisce
      DELETE in `document_sync_outbox` (cancella tutte le versioni
      S3, item 32) + tenta rimozione blob locale best-effort.
      Cerchi solo-utente: hard-delete cascade
      timeline/questions/updates/invites/circle. Cerchi condivisi:
      rimuove SOLO la membership, i dati restano agli altri membri.
      Refresh tokens dell'utente revocati. Idempotente: secondo
      EraseAsync su tombstone gia' presente e' no-op.
    - **Audit log INTOCCATO**: nessuna anonymization di
      `audit_log_entries` ne' `security_audit_log_entries`. La
      tabella audit_log e' gia' WORM-protected via revoke
      DELETE/UPDATE (item 17), quindi tecnicamente nemmeno
      potremmo. Trade-off GDPR: art. 17(3)(e) permette di
      mantenere PII nei record di audit per "establishment,
      exercise or defence of legal claims". Documentato nella
      privacy policy.
    - Endpoint `DELETE /api/account` (esistente, semantica nuova):
      body `{ CurrentPassword, TwoFactorCode?, Confirmation }`.
      Richiede password verificata; se 2FA attivo richiede anche
      TOTP o recovery code (consumato). `Confirmation` deve essere
      esattamente `"ERASE"` (anti-fat-finger). Rate-limit
      `auth-sensitive`. Delega a `IUserErasureService`.
    - CLI amministrativa: `accanto erase-user <userId> --reason "..."
      [--yes]` in [`Accanto.Cli/Program.cs`](../backend/src/Accanto.Cli/Program.cs).
      Pensata per account compromessi (utente non puo' loggarsi
      per usare l'endpoint), richieste legali, support escalation.
      Conferma interattiva "ERASE" salvo `--yes`. Connessione DB
      con `ConnectionStrings:PostgresMigrator` (owner privileges)
      perche' la cancellazione tocca tabelle multiple.
    - Test: 3 nuovi `UserErasureServiceTests` (tombstone PII
      cleared, idempotenza, cascade documenti->outbox DELETE) + 5
      `AccountServiceTests` aggiornati alla nuova semantica
      (Confirmation richiesta, tombstone su cerchio condiviso,
      cerchio condiviso non viene distrutto). Suite verde 173/173.
    Effetto sicurezza/compliance: chiude la classe "richiesta GDPR
    art. 17 non gestita end-to-end" — la cancellazione dei blob
    propaga anche all'offsite cifrato (eliminando tutte le versioni
    S3, non solo l'ultima), e una richiesta di erasure non puo' piu'
    essere bloccata dalla policy conservativa. Documentato il
    razionale del trade-off audit-log-conservato.
      test, account creato fuori dai code-path normali) riceve la
      deadline alla prima request, calcolata su `OwnerGraceHours`.
    - Hook su promozione runtime:
      [`CareCircleService.CreateAsync`](../backend/src/Accanto.Application/CareCircles/CareCircleService.cs)
      (creatore del cerchio) e
      [`InviteService.AcceptAsync`](../backend/src/Accanto.Application/Invites/InviteService.cs)
      (invitato come Owner) chiamano
      [`IOwnerTwoFactorOnboarding`](../backend/src/Accanto.Application/Auth/TwoFactor/OwnerTwoFactorOnboarding.cs)
      che setta la deadline (se assente) e invia l'email
      `TwoFactorRequiredForOwner` con la data limite.
    - Notifiche email (3 totali, minime, via
      `ICircleEmailNotifier.SendSecurityEmailAsync` che bypassa
      preferenze topic):
      `TwoFactorRequiredForOwner` (alla promozione),
      `TwoFactorEnabled` (post enable, "se non sei stato tu..."),
      `TwoFactorDisabled` (post disable). NIENTE reminder
      schedulato o hosted-service: per scelta esplicita di
      scope minimo. Il banner frontend si arrangia con
      `X-2FA-Required-By`.
    - Flag di backout: `TwoFactor:RequireForOwners=false` (default
      true) disattiva l'intero enforcement runtime senza ridistribuire.
      Utile come kill-switch operativo se emergesse un edge case
      in produzione.
    - 6 nuovi integration test in
      [`TwoFactorOwnerEnforcementTests`](../backend/tests/Accanto.Tests/TwoFactorOwnerEnforcementTests.cs):
      Owner entro grace (200 + header), Owner oltre grace (403 +
      problem code), 2fa/setup raggiungibile oltre grace, logout
      raggiungibile oltre grace, non-Owner mai bloccato, lazy
      backfill funzionante. Totale 167/167 PASS.
    - **Out of scope (deliberato per minimal)**: forzare 2FA
      *anche entro la grace* sui soli endpoint distruttivi (delete
      circle, change role, owner-only invite). Da rivalutare se
      durante la grace dovesse capitare un incident reale di
      account takeover su Owner senza 2FA configurata.
    Effetto sicurezza: chiude la classe di compromise "Owner senza
    2FA → password leakata → game-over cerchio". Ruolo piu'
    privilegiato finalmente protetto al livello che merita, con
    rollout senza lockout grazie a backfill + grace.

## Storico run

| Data | Tag | Esito | Note |
|---|---|---|---|
| 2026-06-03 | v0.8.0 | 5 HIGH (nginx base) + 1 HIGH (caddy upstream) + 1 falso positivo gitleaks | Patch applicata in v0.8.1. |
| 2026-06-03 | main post-hardening | ZAP: 0 FAIL, WARN da 8→4 (frontend), 7→3 (web), 1 (backend) | Aggiunti header sicurezza nginx (defense-in-depth). |
| 2026-06-03 | main post-IDOR-probe | 21/21 PASS su probe tenant isolation | Nessun IDOR su endpoint scoped a `care-circles/{id}`. |
| 2026-06-03 | main post-hardening immagini | 0 HIGH/CRITICAL su tutte e 3 le immagini; probe IDOR 21/21 PASS | Backend → chiseled (`app`/UID 1654, no shell). Frontend+web → `nginx-unprivileged` (`nginx`/UID 101, porta 8080). |
| 2026-06-03 | main post-supply-chain | Dependabot attivo, CodeQL attivo, container runtime hardening, Caddyfile audit | Defense-in-depth a livello supply chain + runtime + edge proxy. Probe IDOR ancora 21/21 PASS sullo stack hardened. |
| 2026-06-04 | main post-tier2 | `security.txt` pubblicato, CSP/COOP/CORP anche a livello nginx, probe RBAC 23/23 PASS, probe rate-limit 4/4 PASS | Coverage spostata da "perimetro + tenant" a "perimetro + tenant + ruoli + rate-limit". |
| 2026-06-04 | main post-tier3 | JWT HS256-only fail-fast, split ruoli Postgres `accanto`/`accanto_app` con `REVOKE CREATE`, upload magic-bytes + ClamAV opzionale, probe upload 5/5 PASS, RBAC 23/23 PASS, tenant 21/21 PASS, unit 131/131 PASS | Tier 3 completo. Difesa in profondità su auth/DB/upload. |
| 2026-06-04 | main post-tier3-ci | Workflow `zap-full-auth` (full scan autenticato, schedulato settimanale + manual dispatch) aggiunto in CI | Coverage DAST passa da baseline manuale a full scan autenticato pianificato. |
| 2026-06-04 | main post-tier3-hardening | Tabelle audit append-only via `REVOKE UPDATE,DELETE` su `accanto_app` (verifica manuale: `permission denied for table audit_log_entries`); ordine middleware corretto → 403 loggati come 403, non più come 500; RBAC 23/23 PASS, unit 131/131 PASS | Quick wins post-audit: difesa in profondità su audit + osservabilità log. |
| 2026-06-04 | main post-backup-drill | Backup cifrato (`pg_dump -Fc` + AES-256-CBC PBKDF2 600k iter) e restore drill end-to-end (Postgres effimero tmpfs, 13 sanity check) implementati e validati. Primo drill: 13/13 PASS. Runbook DR completo. | Backup era teorico (best-effort `pg_dump`), ora c'è procedura cifrata + drill ripetibile + RTO/RPO documentati. |
| 2026-06-04 | main post-secret-runbook | Secret rotation runbook formalizzato (7 segreti inventariati, procedura per-segreto, compromise scenario, drill annuale calendarizzato). | Conoscenza tribale → procedura scritta. Pronto per drill primo lunedì di gennaio. |
| 2026-06-04 | main post-edge-ratelimit | Caddy custom con `mholt/caddy-ratelimit` + 3 zone per-IP sliding window su `/auth/login` (30/min), `/auth/refresh` (60/min), `/auth/register` (5/h). Test funzionale: 5 register PASS, 6°–7° → 429. | Defense-in-depth contro credential stuffing distribuito su molti username dalla stessa macchina (il rate-limit applicativo per-utente non scatterebbe in quello scenario). |
| 2026-06-04 | main post-dep-scan | Job `dotnet-deps` + `npm-audit` (matrix frontend/web) in CI con allowlist tracciata. Stato: backend 0 vuln, frontend solo moderate, web 1 high tollerata (Astro server-islands XSS, non esploitabile in build statico). | Trivy copriva solo OS+lib del runtime; ora coperta anche supply chain delle dipendenze sorgente. Allowlist con scadenza forza il follow-up. |
| 2026-06-04 | main post-jwt-multikid | JWT multi-key con `IssuerSigningKeyResolver`: schema `Jwt__Keys__<id>` + `Jwt__ActiveKeyId`, claim `kid` nell'header, validazione kid-aware con fallback per token legacy. Backward compat su `Jwt__Key`. Test 141/141 PASS (10 nuovi `JwtSigningMaterialTests`). Runbook secret-rotation §3 con procedura zero-downtime. | Ultimo TODO della sezione secret-rotation chiuso: ora ogni segreto Accanto ha procedura di rotazione zero-downtime o quasi (eccetto BACKUP_PASSPHRASE per ovvi motivi di retention). |
| 2026-06-04 | main post-forensic | `forensic-snapshot.ps1` (bundle .tar.gz: DB enc + audit CSV 30gg + refresh attivi + users summary + docker inspect + immagini digest + log 72h + sha256 manifest). `backup-offsite.ps1` + `.env.backup-offsite.example` (wrapper amazon/aws-cli per S3-compat, IONOS placeholder, idempotente). Compromise scenario aggiornato: step 0 = forensic snapshot PRIMA di toccare segreti. | Risolve "rotare segreti distrugge prove volatili"; aggancia il futuro upload offsite IONOS senza dover scrivere codice quando il piano cloud sara' attivo. |
| 2026-06-04 | main post-csp-reporting | Endpoint `/api/security/csp-report` (anonimo, rate-limit 100/min IP, body cap 8KB, accetta sia `application/csp-report` legacy che `application/reports+json` Reporting API moderno) → log strutturati `Accanto.Security.Csp`. Caddyfile vetrina + SPA aggiungono `report-uri` + `report-to csp-endpoint` + header `Reporting-Endpoints` puntando all'endpoint. Test 146/146 PASS (5 nuovi `CspReportEndpointTests`: legacy, Reporting API, JSON invalido, body vuoto, anonimo). | Le CSP sono gia' in enforce dal day-1; mancava il canale per RACCOGLIERE le violazioni reali (es. estensioni browser, mixed content, regressioni del frontend dopo refactor). Ora ogni violazione finisce in log strutturato per ASR-tuning. |
| 2026-06-04 | main post-sbom | Job `sbom` in security.yml genera SBOM CycloneDX 1.5 JSON per backend (.NET, runtime-only) + frontend + web ad ogni push/PR/schedulazione (upload come workflow artifact, retention 90 gg). release.yml rigenera e allega le SBOM come asset della GitHub Release con sidecar `.sha256`. | Da "scan-and-fail" a "inventario versionato": consumabile da Dependency-Track/Grype/OSV-Scanner per re-scan offline su nuove CVE, e richiesto da clienti enterprise/sanitario (NTIA + EO 14028). |
| 2026-06-04 | main post-slsa | SLSA build provenance per le 3 immagini GHCR: BuildKit attestation (`provenance: mode=max` + `sbom: true`) embedded nell'OCI manifest + GitHub-signed attestation via Sigstore keyless (`actions/attest-build-provenance@v2`, push-to-registry). Verifica consumer: `gh attestation verify oci://ghcr.io/.../accanto-<img>:<tag> --repo AndreaPrestia/Accanto`. | Mitigazione tipo-squatting + dependency-confusion: criptograficamente verificabile che l'immagine viene da questa repo e da questo workflow, non da un look-alike pushato altrove. Allineato a SLSA L3. |
| 2026-06-04 | main post-logging | `LogContextEnrichmentMiddleware` arricchisce ogni request con `UserId`/`ClientIp`/`RequestId` nel Serilog `LogContext`; backend in compose riceve `Logging__SeqUrl` come opt-in; runbook `logging.md` con query sicurezza, retention, esposizione prod (VPN/auth/OIDC). | Da `grep` su `docker logs` a indagine indicizzata multi-dimensionale (user × ip × time × event). Cardine per il prossimo step su monitoring + alerting su pattern sospetti. |
| 2026-06-04 | main post-upload-hardening | `FileSignatureValidator.Validate` introdotto: 3 livelli di difesa (coerenza estensione/content-type, rifiuto universale di magics pericolosi MZ/ELF/Mach-O/ZIP/gzip/RAR/7z, validazione strutturale per formato — PDF `%%EOF`, PNG `IHDR`, JPEG `EOI`, UTF-8 strict per `text/plain`). 16 nuovi unit test, 2 nuove probe end-to-end (totale 7). Test 161/161 PASS. | Chiude polyglot upload e extension confusion; sommato a magic-bytes head-only + AV (item 15) il livello di difesa su upload diventa coerente con il resto della superficie. |
| 2026-06-04 | main post-monitoring | Runbook `monitoring.md`: Healthchecks.io per job interni + UptimeRobot per endpoint pubblici (`/`, `app/`, `/api/health/ready`). Dead-man's switch implementato in `backup.ps1` e `restore-drill.ps1` (env `HEARTBEAT_BACKUP_URL` / `HEARTBEAT_RESTORE_URL`, ping POST opt-in solo a fine job riuscito). | Auto-osservazione (Seq) non basta: serve sonda esterna indipendente dall'infra monitorata. Chiude la classe "cron silenziosamente fallito" sui job DR critici. |
| 2026-06-04 | main post-2fa-owner-enforcement | `User.TwoFactorRequiredFromUtc` + migration con backfill SQL per Owner esistenti (deadline NOW+7gg). `RequireTwoFactorForOwnersMiddleware`: 403/`two_factor_required_for_owner` oltre grace, header `X-2FA-Required-By` entro. Whitelist minima (`/api/account/2fa/*`, auth, health, swagger). 3 notifiche email security (`TwoFactorRequiredForOwner`/`Enabled`/`Disabled`) via `SendSecurityEmailAsync` (bypassa preferenze topic). Hook su `CareCircleService.CreateAsync` + `InviteService.AcceptAsync`. Flag `TwoFactor:RequireForOwners` per backout. Test 167/167 PASS (6 nuovi `TwoFactorOwnerEnforcementTests`). | Chiude la classe "Owner senza 2FA → password leakata → game-over cerchio": il ruolo piu' privilegiato finalmente protetto. Rollout senza lockout grazie a backfill + grace 7gg + whitelist deadlock-free. |
| 2026-06-19 | main post-s3-dual-write | `document_sync_outbox` + `DocumentSyncWorker` (BackgroundService) + `S3DocumentReplica` (PutAsync blob cifrato, DeleteAllVersionsAsync su bucket versionato). Outbox enqueue nella stessa transazione di `DocumentService.Upload/Delete` → DB e S3 coerenti. Gating completo via `S3DocumentReplica:Enabled` (default off, zero deps in dev/test). Bucket IONOS `accanto-backups/storage/` senza Object Lock (compatibile GDPR), `/backups/*` con Object Lock 7y. Test 171/171 PASS (2 nuovi `DocumentSyncOutboxTests`). | Da single-point-of-failure (disk locale) a copia geografica cifrata best-effort. RPO ≈ 10s + worker latency. Chiave cifratura resta solo lato app → bucket leak non decifra nulla. |
| 2026-06-19 | main post-gdpr-erasure | `IUserErasureService` + endpoint `DELETE /api/account` riscritto in modalita' tombstone: PII azzerati (`erased-{shortId}@accanto.invalid`, password vuota, 2FA segreti rimossi), refresh tokens revocati, **audit log intatto** (GDPR 17(3)(e)). Cascade documenti via outbox DELETE → tutte le versioni S3 cancellate. Cerchi solo-utente hard-deleted; cerchi condivisi conservati per altri membri (rimossa solo la membership). Endpoint richiede password + (se 2FA) TOTP/recovery + `Confirmation == "ERASE"`. CLI `accanto erase-user <userId> --reason "..." [--yes]` per admin/legal. Migration `AddUserErasure` (`IsErased`/`ErasedAt`/`ErasureReason`). Test 173/173 PASS (3 nuovi `UserErasureServiceTests`, 5 `AccountServiceTests` aggiornati alla nuova semantica). | Chiude "richiesta GDPR art. 17 non gestita end-to-end": rimossa la vecchia conservative policy che bloccava la cancellazione su cerchio condiviso (incompatibile con il diritto all'oblio), e ora l'erasure propaga anche all'offsite S3 cancellando ogni versione (non solo l'ultima). |
