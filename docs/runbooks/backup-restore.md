# Runbook — Backup & Restore del database Accanto

> **Scope**: tutto lo stato persistente vive in Postgres (`db` service del compose). Storage file (`storage/`) e' backupato separatamente — vedi sezione dedicata in fondo.

## Obiettivi (SLO)

| Metrica | Target | Rationale |
|---|---|---|
| **RPO** (Recovery Point Objective) | ≤ 24 h | Backup giornaliero notturno (cron 03:00 UTC). |
| **RTO** (Recovery Time Objective) | ≤ 1 h | Restore drill mensile valida che il tempo end-to-end (download offsite + decrypt + restore + smoke) sta sotto questa soglia. |
| **Retention** | 7 daily + 4 weekly + 12 monthly + 7 yearly | Bilancia spazio (~50 backup totali) e capacita' di recuperare da corruzione lenta scoperta in ritardo. |
| **Drill cadence** | mensile (1° lunedi del mese) | Senza drill testato il backup e' teorico. |

## Componenti

- [scripts/db/backup.ps1](../../scripts/db/backup.ps1) — `pg_dump -Fc` + AES-256-CBC PBKDF2 600k iter via `alpine/openssl`. Output: `backups/accanto-YYYYMMDD-HHMMSS.dump.enc` + `.sha256`.
- [scripts/db/restore-drill.ps1](../../scripts/db/restore-drill.ps1) — Decifra → restore su Postgres effimero tmpfs (porta 55432) → 12+ sanity check SQL → tear-down. Exit code 0 = PASS, 2 = FAIL.
- Storage offsite: a scelta (S3, Backblaze B2, rclone-supported). Lo script `backup.ps1` produce un file locale; l'upload offsite e' un passo separato (vedi cron sotto).

## Setup iniziale

1. **Genera la backup passphrase** (UNA volta, conservala separata dal repo):

   ```powershell
   $pw = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 48 | % { [char]$_ })
   # Salvala SUBITO in 1Password / Bitwarden / Vault sotto voce "Accanto / backup encryption / 2026".
   # Rotazione: ogni 12 mesi (vedi secret-rotation.md).
   ```

2. **Configura lo storage offsite**. Esempio con Backblaze B2 + `rclone`:

   ```powershell
   # Una volta:
   rclone config   # aggiungi remote "b2-accanto-backups" con bucket privato versionato
   # Test:
   rclone lsd b2-accanto-backups:accanto-backups
   ```

3. **Setup permessi sul bucket**:
   - Object-lock / immutability: ON, default retention 7 anni (compliance).
   - Versioning: ON.
   - Bucket-level encryption: ON (in aggiunta a quella applicativa — defense-in-depth).
   - Access key dedicata, scope: solo write/list su questo bucket, NO delete.

## Backup manuale (ad-hoc)

```powershell
$env:BACKUP_PASSPHRASE = (Get-Content ./secrets/backup.pass -Raw).Trim()  # o leggi da password manager
./scripts/db/backup.ps1
# Output:
#   backups/accanto-20260604-093000.dump.enc
#   backups/accanto-20260604-093000.dump.enc.sha256
```

Verifica immediata che il backup sia ripristinabile:

```powershell
./scripts/db/restore-drill.ps1
```

Se PASS, carica offsite:

```powershell
rclone copy ./backups/accanto-20260604-093000.dump.enc     b2-accanto-backups:accanto-backups/ --progress
rclone copy ./backups/accanto-20260604-093000.dump.enc.sha256 b2-accanto-backups:accanto-backups/
```

## Backup schedulato (produzione)

### Linux / cron (host produzione)

```cron
# /etc/cron.d/accanto-backup
# Daily 03:00 UTC: dump + offsite upload. Log su syslog.
0 3 * * * accanto BACKUP_PASSPHRASE_FILE=/etc/accanto/backup.pass \
    /opt/accanto/scripts/db/backup.sh 2>&1 | logger -t accanto-backup
```

### Windows Task Scheduler (dev/staging)

```powershell
$action = New-ScheduledTaskAction -Execute 'pwsh.exe' `
    -Argument '-NoProfile -File C:\accanto\scripts\db\backup.ps1' `
    -WorkingDirectory 'C:\accanto'
$trigger = New-ScheduledTaskTrigger -Daily -At 3am
$principal = New-ScheduledTaskPrincipal -UserId 'NT AUTHORITY\SYSTEM' -RunLevel Highest
Register-ScheduledTask -TaskName 'accanto-backup' -Action $action -Trigger $trigger -Principal $principal
```

> NB: lo script PowerShell e' la reference; per Linux esiste in calendario un follow-up per port a `backup.sh`. Per ora in prod si usa pwsh (cross-platform) o si replica la logica con `docker exec ... pg_dump | docker run alpine/openssl enc ...`.

## Restore drill mensile

```powershell
# Primo lunedi del mese, lancia il drill sull'ultimo backup:
$env:BACKUP_PASSPHRASE = (Get-Content ./secrets/backup.pass -Raw).Trim()
./scripts/db/restore-drill.ps1 -MinUsers 10   # tarare sul baseline reale prod
# Exit 0 = PASS, 2 = FAIL. Annota il risultato nello storico (sezione sotto).
```

**Cosa verifica il drill**:

1. Integrita' file via `SHA256` sidecar.
2. Decifratura con passphrase corrente (rileva passphrase ruotata e dimenticata).
3. `pg_restore` su Postgres effimero (rileva dump corrotto/incompleto).
4. Esistenza delle 8 tabelle critiche (`users`, `care_circles`, `care_circle_members`, `timeline_entries`, `medical_documents`, `audit_log_entries`, `security_audit_log_entries`, `refresh_tokens`).
5. Row count `users` ≥ soglia (rileva dump vuoto o di DB sbagliato).
6. `__EFMigrationsHistory` non vuoto (schema versionato).
7. Ultima migration leggibile.
8. Nessuna FK orfana tra `care_circle_members` e `care_circles`.

Il container temporaneo usa **tmpfs** su `/var/lib/postgresql/data`: niente dati su disco host, tear-down istantaneo.

## Restore reale (disaster recovery)

> **Pre-flight**: prima di toccare il DB di produzione, fai un dump dello stato corrente (anche se compromesso) per analisi forense.

1. **Mettere l'app in maintenance mode**: ferma il backend (`docker compose stop backend`) o esponi una pagina 503 dal proxy Caddy.

2. **Backup forense del DB corrente** (anche se rotto):

   ```powershell
   ./scripts/db/backup.ps1
   Rename-Item backups/accanto-*.dump.enc backups/accanto-PRE-DR-$(Get-Date -f yyyyMMddHHmm).dump.enc
   ```

3. **Scarica il backup offsite scelto**:

   ```powershell
   rclone copy b2-accanto-backups:accanto-backups/accanto-20260603-030000.dump.enc ./backups/ --progress
   rclone copy b2-accanto-backups:accanto-backups/accanto-20260603-030000.dump.enc.sha256 ./backups/
   ```

4. **Valida il backup prima di toccare prod**:

   ```powershell
   ./scripts/db/restore-drill.ps1 -BackupFile ./backups/accanto-20260603-030000.dump.enc
   # SOLO se PASS, prosegui.
   ```

5. **Decifratura + restore su prod**:

   ```powershell
   # Decifra:
   docker run --rm -e PASS=$env:BACKUP_PASSPHRASE -v ${PWD}/backups:/work `
       alpine/openssl enc -aes-256-cbc -pbkdf2 -iter 600000 -d -pass env:PASS `
       -in /work/accanto-20260603-030000.dump.enc -out /work/restore.dump

   # DROP & recreate del DB (DESTRUCTIVE — assicurati che il backup forense allo step 2 sia salvo):
   docker exec -e PGPASSWORD=$env:POSTGRES_PASSWORD accanto-db-1 `
       psql -U accanto -d postgres -c "DROP DATABASE accanto; CREATE DATABASE accanto OWNER accanto;"

   # Riapplica gli init script (ruolo accanto_app):
   docker exec accanto-db-1 sh /docker-entrypoint-initdb.d/01-app-role.sh

   # Restore:
   docker cp ./backups/restore.dump accanto-db-1:/tmp/restore.dump
   docker exec -e PGPASSWORD=$env:POSTGRES_PASSWORD accanto-db-1 `
       pg_restore -U accanto -d accanto --no-owner --no-privileges --exit-on-error /tmp/restore.dump
   docker exec accanto-db-1 rm /tmp/restore.dump
   Remove-Item ./backups/restore.dump
   ```

6. **Smoke test post-restore**:

   ```powershell
   docker compose up -d backend
   # Aspetta che parta, poi:
   ./scripts/security/rbac-probe.ps1     # 23/23 PASS
   ./scripts/security/tenant-probe.ps1   # 21/21 PASS (se esiste)
   curl http://localhost:8080/health/ready  # 200
   ```

7. **Esci da maintenance** e comunica nel canale incident.

## Storage file (`storage/`)

I file caricati da `DocumentService` (PDF medicali, immagini) vivono in `./storage` sul host, montato come bind mount. **Non sono nel backup Postgres** (sarebbe stupido: pg_dump li replicherebbe come blob in audit log indiretti).

- **Backup**: `rclone sync ./storage b2-accanto-storage:accanto-storage --backup-dir b2-accanto-storage:trash/$(date +%F)` schedulato ogni 6h.
- **Encryption**: i file sono gia' criptati lato applicativo via `IFieldProtector` per quanto riguarda i metadati nel DB. I blob su disco NON sono cifrati at-rest dall'app — affidiamoci alla cifratura del filesystem (LUKS in prod) + cifratura bucket-level offsite.
- **Restore**: `rclone sync b2-accanto-storage:accanto-storage ./storage`. Idempotente.

## Storico drill

| Data | Backup testato | Esito | Tempo end-to-end | Note |
|---|---|---|---|---|
| 2026-06-04 | accanto-20260604-090121 | PASS 13/13 | ~25 s (DB dev ~30KB) | Primo drill post-setup. Baseline. Container effimero tmpfs, decrypt + pg_restore + 13 sanity check + tear-down. |

## Rotazione passphrase

Vedi [secret-rotation.md](secret-rotation.md) — la `BACKUP_PASSPHRASE` rientra nel pool da ruotare ogni 12 mesi. Mai cancellare la vecchia passphrase prima di aver ricifrato gli ultimi 12 backup (o averli marcati come "non recuperabili" e dropparli dalla retention).
