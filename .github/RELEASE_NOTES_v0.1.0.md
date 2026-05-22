# Accanto v0.1.0 — prima release pubblica

Prima release pubblica di **Accanto**, un compagno digitale sobrio e mobile-first per chi assiste una persona cara. Self-hostable, AGPL-3.0, italiano, senza telemetria.

## In questa release

- **Cerchi di cura** — uno spazio per ogni persona assistita, con ruoli `Coordinatore`, `Caregiver`, `In ascolto`.
- **Diario** — eventi, sintomi, appuntamenti, decisioni, note personali, con tag e visibilità.
- **Documenti** — referti, esami, prescrizioni; **cifrati a riposo** (AES-256-GCM) prima di toccare il disco.
- **Domande per il medico** — accumulare domande tra una visita e l'altra, con suggerimenti per categoria.
- **Aggiornamenti per gli altri** — messaggi pronti da copiare, con modelli in italiano ispirati a frasi davvero usate dai caregiver.
- **Giornata difficile** — piccoli gesti concreti, da aprire quando tutto pesa.
- **PWA installabile**, mobile-first, offline parziale.
- **Cifratura a riposo end-to-end applicativa**: titolo/contenuto diario, domande/note al medico, aggiornamenti famiglia, descrizione cerchio, blob documenti — tutto AES-256-GCM con chiave master locale.
- **Self-host con TLS automatico** via `docker-compose.prod.yml` + Caddy + Let's Encrypt.

## Come si prova

```sh
git clone https://github.com/AndreaPrestia/Accanto.git
cd Accanto
cp .env.example .env
# imposta Jwt__Key (≥32 char), POSTGRES_PASSWORD, Encryption__MasterKey (openssl rand -base64 32)
docker compose up --build
```

Frontend: http://localhost:5173 — Swagger: http://localhost:8080/swagger

Per il deploy in produzione (HTTPS automatico su un dominio): vedi sezione [Deploy in produzione](https://github.com/AndreaPrestia/Accanto#deploy-in-produzione-con-tls-automatico-via-caddy) del README.

## Scritto con un agente AI

Accanto è scritto end-to-end con l'aiuto di **Claude** (Anthropic) usato come agente di sviluppo: architettura, backend, frontend, test, infra, copy. La scelta è deliberata — il bisogno reale dei caregiver non aspetta. Il codice è qui, leggibile e auditabile.

## Prossimi passi (v0.2)

- Invito di altri caregiver via link al cerchio
- Filtri data nel diario
- Gestione account (cambio email/password, eliminazione)
- Esportazione PDF del cerchio
- Notifiche push PWA per promemoria visite

## Sicurezza

Per segnalazioni di vulnerabilità leggi [SECURITY.md](https://github.com/AndreaPrestia/Accanto/blob/main/SECURITY.md). Per contribuire: [CONTRIBUTING.md](https://github.com/AndreaPrestia/Accanto/blob/main/CONTRIBUTING.md).

---

**Licenza**: AGPL-3.0. Chiunque modifichi Accanto e lo offra come servizio web ad altri deve a sua volta rendere disponibile il codice sorgente. Sembra giusto, per uno strumento che tocca i momenti più delicati della vita delle persone.
