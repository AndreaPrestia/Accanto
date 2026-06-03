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

Cache trivy persistente su volume named `trivy-cache` per evitare di
ri-scaricare il DB CVE a ogni run.

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
- **frontend / web**: 1 WARN `CIS-DI-0001` (container gira come root).
  Nginx master è root ma i worker droppano privilegi su `nginx` user
  automaticamente — rischio accettato. Per chiuderlo serve riconfigurare
  i container per ascoltare su porta non privilegiata (es. 8080) e
  aggiornare il `Caddyfile` upstream. Tracciato come miglioria futura.
- 1 FATAL `CIS-DI-0010` (KEY_SHA512) su frontend/web: **falso positivo**
  (chiave GPG di verifica APK nei layer di base nginx, non un secret
  applicativo).

## Falsi positivi accettati

| Tool | Finding | File / contesto | Motivazione |
|---|---|---|---|
| gitleaks | `generic-api-key` valore `test-key-very-long-test-key-very-long-1234` | `backend/tests/Accanto.Tests/AccantoFactory.cs:18` | Chiave fittizia usata solo dai test di integrazione. Non concede alcun accesso. |
| dockle | `CIS-DI-0010` su `KEY_SHA512` ENV | immagini `accanto-frontend`, `accanto-web` | Variabile ereditata dal layer base `nginx:1.27-alpine`, usata per verificare le firme APK. Non è un secret applicativo. |
| dockle | `CIS-DI-0001` last user is root | immagini `accanto-frontend`, `accanto-web` | Nginx master gira come root ma forka worker non privilegiati. Vedi nota sopra. |

## Miglioramenti tracciati

1. Spostare nginx (frontend + web) su porta non privilegiata e attivare
   `USER nginx` per chiudere `CIS-DI-0001`. Richiede update di
   `nginx.conf`, `EXPOSE`, healthcheck e `Caddyfile` upstream.
2. Aggiungere uno scan ZAP baseline contro lo stack `docker compose`
   locale, autenticato su 2 tenant di prova, per coprire IDOR / authz
   sui cerchi di cura. Più alto ROI applicativo del solo CVE scan.
3. Wiring degli scan trivy + gitleaks in GitHub Actions
   (`.github/workflows/security.yml`) per failo automatico su PR e tag.
4. Valutare il passaggio del backend a `mcr.microsoft.com/dotnet/aspnet:10.0-jammy-chiseled`
   per eliminare i file `setuid` ereditati da Ubuntu (oggi flag solo
   INFO su dockle, non bloccante).

## Storico run

| Data | Tag | Esito | Note |
|---|---|---|---|
| 2026-06-03 | v0.8.0 | 5 HIGH (nginx base) + 1 HIGH (caddy upstream) + 1 falso positivo gitleaks | Patch applicata in v0.8.1. |
