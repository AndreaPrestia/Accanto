# Runbook — Secret Rotation

> **Scope**: rotazione pianificata e di emergenza di tutti i segreti che, se compromessi, permetterebbero accesso non autorizzato ai dati Accanto o ne romperebbero l'integrita'.

## Inventario segreti

| Segreto | Dove vive | Blast radius se compromesso | Cadenza rotazione | Difficolta' |
|---|---|---|---|---|
| `POSTGRES_PASSWORD` | `.env` + Postgres `accanto` role | Letture + DDL su tutto il DB → game over | 12 mesi | Bassa (ALTER ROLE + restart migrator) |
| `POSTGRES_APP_PASSWORD` | `.env` + Postgres `accanto_app` role | Letture + INSERT su tutto il DB; NO drop/alter (revoke + audit append-only) | 12 mesi | Bassa (ALTER ROLE + rolling restart backend) |
| `Jwt__Key` | `.env` | Forge di JWT validi per qualsiasi utente | 6 mesi (o emergenza) | Media (oggi single-key → logout forzato; vedi sezione "miglioramento futuro") |
| `Encryption__MasterKey` / `Encryption__Keys__<id>` | `.env` | Decifratura di tutti i campi at-rest (note diario, documenti, ecc.) | 12 mesi | Media (`accanto-cli rotate-keys` riscrive in background, supporta multi-chiave nativamente) |
| `BACKUP_PASSPHRASE` | password manager (non in `.env`) | Decifratura di tutti i dump offsite | 12 mesi | Alta (ricifrare backup retention attiva — vedi sotto) |
| `Logging__SeqApiKey` (opt-in) | `.env` se osservabilita' attiva | Spam log + lettura log su istanza Seq | 12 mesi | Bassa |
| `Cloud provider keys` (S3/B2 backup, hosting) | password manager + IAM | Lettura/cancellazione backup offsite | 6 mesi | Bassa (rotazione gestita dalla console provider) |

> NB: `Jwt__Issuer` e `Jwt__Audience` NON sono segreti — sono identificativi pubblici.

## Pre-rotation checklist (sempre, ogni rotazione)

- [ ] Backup recente (≤ 24h) verificato con [restore-drill.ps1](../../scripts/db/restore-drill.ps1) — esito PASS.
- [ ] Nuovo segreto generato con entropia adeguata (vedi sezione "Generazione").
- [ ] Nuovo segreto salvato nel password manager (1Password / Bitwarden / Vault) PRIMA di essere applicato.
- [ ] Finestra di manutenzione comunicata se la procedura richiede downtime o logout forzato.
- [ ] Rollback plan a portata di mano (il vecchio segreto resta in vault per ≥ 30 giorni dopo la rotazione).

## Generazione

Tutti i segreti di Accanto sono base64 o random alfanumerici a 32+ byte:

```powershell
# 256-bit random base64 (consigliato per Jwt__Key, Encryption__MasterKey, BACKUP_PASSPHRASE)
docker run --rm alpine/openssl rand -base64 32

# 32+ char alfanumerico (per Postgres password, alcuni provider non amano caratteri speciali in connection string)
-join ((48..57)+(65..90)+(97..122) | Get-Random -Count 48 | % { [char]$_ })

# Encryption key con keyId (per multi-chiave rotation)
$keyId = (Get-Date -f 'yyyyMM')                      # es. "202606"
$key   = docker run --rm alpine/openssl rand -base64 32
"Encryption__Keys__$keyId=$key`nEncryption__ActiveKeyId=$keyId" | clip
```

---

## Procedure per segreto

### 1. `POSTGRES_PASSWORD` (ruolo owner `accanto`)

Usato dal migrator EF all'avvio per applicare le migrazioni. Mai usato a runtime (il backend usa `accanto_app`).

```powershell
$NEW = -join ((48..57)+(65..90)+(97..122) | Get-Random -Count 48 | % { [char]$_ })

# 1. Cambia la password nel DB:
$OLD = (Get-Content .env | Select-String "^POSTGRES_PASSWORD=").ToString().Split('=',2)[1]
docker exec -e PGPASSWORD=$OLD accanto-db-1 `
    psql -U accanto -d accanto -c "ALTER ROLE accanto WITH PASSWORD '$NEW';"

# 2. Aggiorna .env (RICORDA: salva $NEW nel password manager PRIMA di chiudere il terminale).
(Get-Content .env) -replace "^POSTGRES_PASSWORD=.*", "POSTGRES_PASSWORD=$NEW" |
    Set-Content .env -Encoding utf8
# Ricorda di aggiornare anche ConnectionStrings__PostgresMigrator se hai un override esplicito.

# 3. Restart backend (rilegge .env e applica al prossimo MigrateAsync — non c'e' connessione persistente dopo migrate):
docker compose up -d backend

# 4. Verifica:
docker logs accanto-backend-1 --tail 20  # nessun errore di autenticazione
./scripts/security/rbac-probe.ps1        # 23/23 PASS
```

**Downtime**: ~10 sec (restart backend). Niente logout utenti.

---

### 2. `POSTGRES_APP_PASSWORD` (ruolo runtime `accanto_app`)

```powershell
$NEW = -join ((48..57)+(65..90)+(97..122) | Get-Random -Count 48 | % { [char]$_ })
$OWNER_PW = (Get-Content .env | Select-String "^POSTGRES_PASSWORD=").ToString().Split('=',2)[1]

# 1. Cambia password (eseguito come owner, accanto):
docker exec -e PGPASSWORD=$OWNER_PW accanto-db-1 `
    psql -U accanto -d accanto -c "ALTER ROLE accanto_app WITH PASSWORD '$NEW';"

# 2. Aggiorna .env:
(Get-Content .env) -replace "^POSTGRES_APP_PASSWORD=.*", "POSTGRES_APP_PASSWORD=$NEW" |
    Set-Content .env -Encoding utf8

# 3. Aggiorna anche ConnectionStrings__Postgres se hai un override esplicito.
# Se usi le default-from-env del compose il refresh e' automatico.

# 4. Restart backend:
docker compose up -d backend

# 5. Verifica:
docker exec -e PGPASSWORD=$NEW accanto-db-1 psql -U accanto_app -d accanto -c "SELECT 1;"  # 1 riga
./scripts/security/rbac-probe.ps1   # 23/23 PASS
```

**Downtime**: ~10 sec.

---

### 3. `Jwt__Key`

> **Limitazione corrente**: il backend valida JWT con UNA sola `IssuerSigningKey` ([JwtTokenService.cs](../../backend/src/Accanto.Infrastructure/Security/JwtTokenService.cs)). La rotazione invalida TUTTI i token in circolazione → tutti gli utenti devono rifare login.
>
> **Miglioramento futuro** (item separato in roadmap): implementare `IssuerSigningKeyResolver` con dictionary `{ keyId → SymmetricSecurityKey }`, header JWT con `kid` claim, e supporto a `Jwt__Keys__<keyId>` + `Jwt__ActiveKeyId` come gia' fatto per `Encryption`. Permette grace period di N minuti dove vecchio e nuovo token convivono.

**Procedura corrente** (con logout forzato):

```powershell
$NEW = docker run --rm alpine/openssl rand -base64 48

# 1. Aggiorna .env:
(Get-Content .env) -replace "^Jwt__Key=.*", "Jwt__Key=$NEW" | Set-Content .env -Encoding utf8

# 2. Restart backend (TUTTI gli access token esistenti diventano invalidi):
docker compose up -d backend

# 3. Invalidate refresh token table per forzare ri-autenticazione completa:
$OWNER_PW = (Get-Content .env | Select-String "^POSTGRES_PASSWORD=").ToString().Split('=',2)[1]
docker exec -e PGPASSWORD=$OWNER_PW accanto-db-1 `
    psql -U accanto -d accanto -c "UPDATE refresh_tokens SET ""RevokedAt"" = now() WHERE ""RevokedAt"" IS NULL;"

# 4. Comunica agli utenti: "Per motivi di sicurezza, ti chiediamo di rifare login. I dati sono intatti."
```

**Downtime UI**: zero per il backend; gli utenti vedono `401` al prossimo refresh → redirect login.

**Frequenza**: ogni 6 mesi (rotazione preventiva) oppure IMMEDIATA in caso di sospetta compromissione.

---

### 4. `Encryption__MasterKey` / multi-key rotation

Questo segreto cifra i dati at-rest (note diario, documenti, metadati sensibili). Il backend supporta gia' multi-chiave nativamente (vedi [README.md § Cifratura a riposo](../../README.md#cifratura-a-riposo)), quindi la rotazione e' **zero-downtime**.

```powershell
# 1. Genera nuova chiave con keyId datato:
$NEW_KEY_ID = (Get-Date -f 'yyyyMM')                # es. "202606"
$NEW_KEY    = docker run --rm alpine/openssl rand -base64 32

# 2. Aggiungi la nuova chiave a .env SENZA rimuovere la vecchia:
Add-Content .env "`nEncryption__Keys__$NEW_KEY_ID=$NEW_KEY"
Add-Content .env "Encryption__ActiveKeyId=$NEW_KEY_ID"
# La vecchia Encryption__MasterKey RESTA (e' la chiave v1 legacy, serve per leggere
# i record non ancora ruotati). Va rimossa SOLO dopo che il rotator ha finito.

# 3. Restart backend (legge nuova config; scritture nuove usano la nuova chiave):
docker compose up -d backend

# 4. Esegui il rotator CLI (riscrive tutti i record esistenti con la nuova chiave):
docker compose exec backend dotnet Accanto.Cli.dll rotate-keys
# Output:
#   Rotazione completata:
#     cerchi di cura      : N
#     voci di diario      : N
#     ...

# 5. Verifica che NON ci siano piu' record cifrati con la vecchia chiave:
$OWNER_PW = (Get-Content .env | Select-String "^POSTGRES_PASSWORD=").ToString().Split('=',2)[1]
docker exec -e PGPASSWORD=$OWNER_PW accanto-db-1 psql -U accanto -d accanto -tA -c "
SELECT 'timeline_entries:v1' AS k, count(*) FROM timeline_entries WHERE ""EncryptedBody"" LIKE 'v1.%'
UNION ALL SELECT 'medical_documents:v1', count(*) FROM medical_documents WHERE ""EncryptedFilename"" LIKE 'v1.%'
;"  # tutti i count devono essere 0

# 6. Rimuovi la vecchia Encryption__MasterKey da .env e restart:
(Get-Content .env) -notmatch "^Encryption__MasterKey=" | Set-Content .env -Encoding utf8
docker compose up -d backend
```

**Downtime**: zero. Gli utenti non si accorgono di nulla.

**Frequenza**: 12 mesi.

---

### 5. `BACKUP_PASSPHRASE`

La passphrase usata da [backup.ps1](../../scripts/db/backup.ps1) per cifrare i dump.

**Problema**: ruotando la passphrase, i backup vecchi restano cifrati con quella precedente. Per recuperarli serve mantenere la vecchia.

**Strategia**: passphrase versionata + retention chiara.

```powershell
# 1. Genera nuova passphrase:
$NEW = docker run --rm alpine/openssl rand -base64 32

# 2. Salva ENTRAMBE nel password manager con label datata:
#    "Accanto backup passphrase / 2025-06 (precedente)" — leggibile per disaster recovery
#    "Accanto backup passphrase / 2026-06 (attiva)"      — usata dal job schedulato

# 3. Aggiorna il segreto usato dal cron/Task Scheduler:
#    Linux: /etc/accanto/backup.pass
#    Windows: secrets/backup.pass (read-only per il task user)

# 4. Esegui un backup di test con la nuova passphrase e verificalo:
$env:BACKUP_PASSPHRASE = $NEW
./scripts/db/backup.ps1
./scripts/db/restore-drill.ps1   # 13/13 PASS richiesto

# 5. Marca nel runbook quando la vecchia passphrase puo' essere distrutta:
#    "passphrase 2025-06 distruttibile dopo $(date d '+ 7 anni')" — ovvero quando
#    l'ultimo backup cifrato con essa esce dalla retention 7-yearly.
#    Fino ad allora, la vecchia resta nel vault con tag "DR-only / read-only".
```

**Frequenza**: 12 mesi.

---

## Compromise scenario (rotazione di emergenza)

Trigger: sospetto leak di `.env`, container compromesso, dipendente uscito con accesso ai segreti, alert di gitleaks su un commit.

**Ordine consigliato** (dal piu' impattante al piu' contenibile):

1. **`Jwt__Key`** IMMEDIATAMENTE → invalida tutte le sessioni. Procedura sezione 3 + comunicazione user "logout forzato per manutenzione sicurezza".
2. **`POSTGRES_APP_PASSWORD`** e **`POSTGRES_PASSWORD`** → impedisce accesso DB con credenziali vecchie. Procedure sezioni 1+2.
3. **`Encryption__MasterKey`** → solo SE c'e' sospetto che la chiave sia stata esfiltrata. Procedura sezione 4 (richiede tempo CLI rotator).
4. **`BACKUP_PASSPHRASE`** → ruota e ricontrolla che i backup offsite non siano stati scaricati da terzi (audit log del provider storage).
5. **Cloud provider keys** → ruota via console IAM, revoca le vecchie.
6. **Forza logout di TUTTI gli utenti** (gia' fatto al passo 1 implicitamente con Jwt rotation, ma verifica `refresh_tokens` siano tutti revocati).
7. **Audit forense**: dump completo di `audit_log_entries` e `security_audit_log_entries` filtrato sull'intervallo del sospetto incident. Cerca pattern anomali (login da IP nuovi, mass-export di documenti, modifiche permessi). Le tabelle sono append-only a livello DB (vedi item 17 in [security-audit.md](../security-audit.md)) → l'attaccante non ha potuto cancellare le proprie tracce.
8. **Post-mortem** entro 7 giorni: timeline incidente, vettore di compromissione, mitigazioni adottate, modifiche al runbook.

## Drill annuale

Una volta l'anno (calendario: **primo lunedi di gennaio**), esegui un **dry-run** completo della procedura su staging:

1. Genera nuovi segreti.
2. Applica le procedure 1-5 in sequenza.
3. Misura il tempo end-to-end (target: < 2 ore per la full rotation).
4. Annota intoppi, aggiorna il runbook.
5. Verifica che il vecchio segreto sia ancora nel vault (non distruggerlo durante il drill).

Annota nel storico sotto.

## Storico rotazioni

| Data | Tipo | Segreto/i ruotato/i | Trigger | Esito | Note |
|---|---|---|---|---|---|
| 2026-06-04 | runbook setup | — | Definizione iniziale procedura | n/a | Runbook creato, drill non ancora eseguito. |
