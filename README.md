# Accanto

> Un compagno digitale, sobrio e mobile-first, per chi assiste una persona cara.

Accanto è un'applicazione web open source pensata per i **caregiver familiari**: quei figli, fratelli, partner e amici che si trovano, spesso da un giorno all'altro, a coordinare visite, terapie, documenti e relazioni intorno a una persona malata o fragile. Non è un'app medica. Non sostituisce un medico, uno psicologo, un'assistente sociale. È uno spazio dove tutto ciò che riguarda l'assistenza — appuntamenti, sintomi, referti, domande per il medico, aggiornamenti per i parenti — può vivere in un solo posto, con un tono gentile e senza fronzoli.

Il progetto è in **italiano** per scelta: la maggior parte dei caregiver italiani usa quotidianamente strumenti generalisti pensati in inglese o per contesti diversi. Accanto vuole sentirsi famigliare nel modo in cui parla.

---

## Indice

1. [Cosa fa](#cosa-fa)
2. [Architettura](#architettura)
3. [Stack tecnico](#stack-tecnico)
4. [Requisiti](#requisiti)
5. [Avvio rapido (Docker)](#avvio-rapido-docker)
6. [Sviluppo locale (senza Docker)](#sviluppo-locale-senza-docker)
7. [API e Swagger](#api-e-swagger)
8. [Privacy e dati](#privacy-e-dati)
9. [Modulo AI futuro](#modulo-ai-futuro)
10. [Roadmap](#roadmap)
11. [Licenza](#licenza)

---

## Cosa fa

Accanto organizza la cura intorno a un **Cerchio di cura** (`CareCircle`): uno spazio dedicato a una persona assistita. Per ogni cerchio puoi:

- **Diario** — annotare eventi, sintomi, appuntamenti, decisioni, note personali, con tag e visibilità (tutto il cerchio o solo io).
- **Documenti** — caricare referti, esami, prescrizioni e scaricarli quando servono. Ogni file viene salvato sul filesystem dell'istanza, mai su servizi terzi.
- **Domande per il medico** — accumulare le domande nei giorni che precedono una visita, con suggerimenti per categoria (diagnosi, terapia, dolore, cure palliative, dimissione, …).
- **Aggiornamenti per gli altri** — comporre messaggi pronti da copiare e inviare a famiglia stretta, parenti, amici. Tre modelli in italiano già inclusi, ispirati a frasi davvero usate dai caregiver.
- **Giornata difficile** — una pagina semplice con piccoli gesti concreti, da aprire quando tutto pesa.

Il **ruolo** di ogni partecipante al cerchio è uno di `Coordinatore` (Owner), `Caregiver`, `In ascolto` (Viewer). I caregiver scrivono, i viewer leggono, il coordinatore può archiviare il cerchio.

L'interfaccia è **mobile-first** e installabile come PWA: aperta dal telefono, si comporta come un'app, anche offline parzialmente (shell e asset cachati).

## Architettura

Monolite modulare in stile Clean Architecture:

```
backend/
  src/
    Accanto.Domain/           # entità ed enum, nessuna dipendenza
    Accanto.Application/      # servizi, DTO, validator, contratti (IAccantoDbContext, IFileStorage, …)
    Accanto.Infrastructure/   # EF Core, PostgreSQL, JWT, PBKDF2, storage locale
    Accanto.Api/              # ASP.NET Core Web API, controller REST, middleware
  tests/
    Accanto.Tests/            # xUnit + InMemory + WebApplicationFactory

frontend/                     # Vite + React 18 + TypeScript + Tailwind + PWA
```

Decisioni esplicite:

- L'`Application` referenzia EF Core via `IAccantoDbContext`. Pragmatismo > purezza assoluta.
- Le entità usano enum stringa via `HasConversion<string>()`; i `Tags` sono `text[]` nativi Postgres (per supportare `Contains` in LINQ).
- Validazione con **FluentValidation** + filter MVC che restituisce `422 Unprocessable Entity`.
- Errori applicativi tipizzati (`NotFoundException`, `ForbiddenException`, `ConflictException`, `AppValidationException`) mappati a HTTP status nel middleware.
- Autorizzazione per cerchio centralizzata in `CareCircleAuthorization`: richiede appartenenza + ruolo minimo (Owner < Caregiver < Viewer per ordinale, più alto = meno privilegi).

## Stack tecnico

**Backend**

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core 10 + Npgsql
- PostgreSQL 16
- JWT bearer (HS256) + PBKDF2 (HMACSHA256, 100k iters, salt 16 byte, hash 32 byte)
- FluentValidation
- Swashbuckle / OpenAPI

**Frontend**

- Vite + React 18 + TypeScript
- Tailwind CSS (palette sobria slate)
- React Router v6
- axios
- `vite-plugin-pwa` (manifest + service worker)

**Infrastruttura**

- Docker / docker-compose (db + backend + frontend dietro nginx)

## Requisiti

- Docker + docker-compose **oppure**
- .NET SDK 10.0.x + Node.js 22.x + PostgreSQL 16

## Avvio rapido (Docker)

1. Copia il file `.env.example` in `.env` e imposta almeno una `Jwt__Key` lunga (≥ 32 caratteri) e una `POSTGRES_PASSWORD` non banale.
2. Avvia lo stack:

   ```sh
   docker compose up --build
   ```

3. Apri il browser:
   - Frontend (PWA): http://localhost:5173
   - Backend health: http://localhost:8080/health
   - Swagger: http://localhost:8080/swagger

Il frontend è servito da **nginx** e fa da reverse proxy a `/api/` verso il backend, quindi tutto passa per `localhost:5173`.

I file caricati vivono in `./storage/` sul filesystem host (volume montato in `/data/storage` dentro il container). Il database vive nel volume `db-data`.

## Sviluppo locale (senza Docker)

### Backend

```sh
cd backend
# Imposta la connessione (oppure modifica appsettings.Development.json)
dotnet run --project src/Accanto.Api
```

In dev, l'app prova ad applicare le migrazioni in automatico all'avvio. Per crearne di nuove:

```sh
cd backend/src/Accanto.Infrastructure
dotnet ef migrations add NomeMigrazione --startup-project ../Accanto.Api
```

Test:

```sh
cd backend
dotnet test
```

### Frontend

```sh
cd frontend
npm install
npm run dev
```

Imposta `VITE_API_BASE_URL=http://localhost:8080/api` se sviluppi il frontend separato dal backend (o usa un proxy in `vite.config.ts`). In produzione (Docker) il valore di default `/api` funziona già.

## API e Swagger

- `POST /api/auth/register` — registrazione + login automatico
- `POST /api/auth/login` — login con email + password
- `GET  /api/auth/me`
- `GET/POST/PUT/DELETE /api/care-circles[/{id}]`
- `GET/POST/PUT/DELETE /api/care-circles/{id}/timeline`
- `GET/POST/DELETE /api/care-circles/{id}/documents` + `/download`
- `GET/POST/PUT/DELETE /api/care-circles/{id}/doctor-questions`
- `GET /api/doctor-question-templates`
- `GET/POST/DELETE /api/care-circles/{id}/shared-updates`
- `GET /api/shared-update-templates`

Swagger UI: `http://localhost:8080/swagger`.

## Privacy e dati

Accanto è progettato per essere **self-hostable** (sul tuo PC, su un piccolo VPS, in una intranet). Non esiste un servizio centralizzato Accanto SaaS. Nessun dato lascia mai l'istanza che gestisci.

- Le password sono salvate solo come hash PBKDF2 con salt unico per utente.
- I documenti restano sul filesystem dell'istanza, mai inviati altrove.
- Nessuna telemetria, nessun tracker, nessun analytics di default.
- I JWT scadono entro la durata configurata (`Jwt__ExpiryMinutes`, default 480 minuti).

### Cifratura a riposo

Trattandosi di dati sensibili (annotazioni cliniche, documenti medici), Accanto cifra a livello applicativo i campi e i file più riservati prima di scriverli su disco:

- **AES-256-GCM** con nonce casuale per record e tag di autenticità (16 byte).
- **Campi DB cifrati**: titolo e contenuto del diario, domande e note per il medico, contenuto degli aggiornamenti famiglia, note e nome originale dei documenti medici, descrizione del cerchio.
- **Blob dei documenti** caricati: cifrati in memoria prima della scrittura su disco; decifrati on-demand al download.
- **Tag e metadati strutturali** (date, tipi, ruoli) restano in chiaro per consentire ricerca e filtri.

La chiave master è una stringa **base64 da 32 byte** fornita via variabile d'ambiente `Encryption__MasterKey`. Generala una volta sola con:

```bash
openssl rand -base64 32
```

> ⚠️ **Attenzione**: se perdi la chiave perdi i dati cifrati. Conservala in un gestore di segreti (vault) o almeno in un backup separato dal database. Non ruotare la chiave in v0.1: non è ancora supportata la migrazione automatica del ciphertext.

**Restano a carico di chi fa il deploy**:

- cifratura del disco (LUKS, BitLocker, dm-crypt sul volume Postgres e sul volume dei documenti);
- terminazione TLS (reverse proxy: Caddy, Traefik, nginx) — il `docker-compose` di esempio espone solo HTTP in locale;
- backup cifrati del database e della directory `storage/`, includendo SEPARATAMENTE la chiave master;
- politiche di accesso al server e rotazione delle credenziali Postgres.

**Disclaimer**: Accanto non è un dispositivo medico, non sostituisce nessuna figura sanitaria, non offre diagnosi né consigli terapeutici. È uno strumento di **organizzazione personale**.

## Modulo AI futuro

Una direzione esplorata per le versioni future è un modulo AI **opzionale e disattivabile** che aiuti il caregiver con:

- riassunti di una settimana di diario, per prepararsi a una visita;
- suggerimenti di domande per il medico, a partire dalle voci recenti;
- riformulazione di un aggiornamento per famigliari, con il tono giusto per il pubblico scelto.

Vincoli che ci diamo già da ora:

- nessuna chiamata AI senza un'azione esplicita dell'utente;
- backend pluggabile: provider locale (es. Ollama) o esterno scelto dall'amministratore dell'istanza;
- nessun invio di dati medici a fornitori AI senza un consenso informato esplicito.

Non è incluso nella versione 0.1.

## Roadmap

**v0.1 (questa release)**
Cerchi di cura, diario, documenti, domande per il medico, aggiornamenti pronti da copiare, giornata difficile. Autenticazione email+password, PWA installabile.

**v0.2**
- Invito di altri caregiver via link a un cerchio esistente.
- Promemoria visite (notifiche push PWA).
- Esportazione del cerchio in PDF (per portare un riassunto al medico).
- Filtri data nel diario.

**v0.3**
- Multilingua (en, es) con copy mantenuto in italiano come base.
- Editing dei tag e visibilità in massa.
- Modulo AI opzionale (riassunti, suggerimenti) con provider configurabile.
- 2FA TOTP.

**v0.4**
- Sezione "cura di chi cura" più ricca: check-in emotivo, suggerimenti contestuali, contatti di supporto regionali.
- Audit trail per cerchi condivisi.
- Backup/restore guidato dell'istanza.

## Licenza

Accanto è rilasciato sotto licenza **GNU Affero General Public License v3.0 (AGPL-3.0)**. Vedi [LICENSE](LICENSE).

Questa scelta è deliberata: chiunque modifichi Accanto e lo offra come servizio web ad altri deve a sua volta rendere disponibile il codice sorgente delle proprie modifiche. Sembra giusto, per uno strumento che tocca i momenti più delicati della vita delle persone.
