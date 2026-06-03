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

## Storico run

| Data | Tag | Esito | Note |
|---|---|---|---|
| 2026-06-03 | v0.8.0 | 5 HIGH (nginx base) + 1 HIGH (caddy upstream) + 1 falso positivo gitleaks | Patch applicata in v0.8.1. |
| 2026-06-03 | main post-hardening | ZAP: 0 FAIL, WARN da 8→4 (frontend), 7→3 (web), 1 (backend) | Aggiunti header sicurezza nginx (defense-in-depth). |
| 2026-06-03 | main post-IDOR-probe | 21/21 PASS su probe tenant isolation | Nessun IDOR su endpoint scoped a `care-circles/{id}`. |
| 2026-06-03 | main post-hardening immagini | 0 HIGH/CRITICAL su tutte e 3 le immagini; probe IDOR 21/21 PASS | Backend → chiseled (`app`/UID 1654, no shell). Frontend+web → `nginx-unprivileged` (`nginx`/UID 101, porta 8080). |
