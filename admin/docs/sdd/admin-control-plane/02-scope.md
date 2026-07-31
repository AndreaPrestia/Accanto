# 02 — Scope

## In scope

### Admin identity

- Admin users separati dagli utenti pubblici.
- Admin roles.
- Admin sessions.
- Admin login.
- Admin logout.
- Admin token refresh.
- Admin `me`.

### Admin database

- Database admin separato.
- Migration admin separata.
- Connection string admin separata.

### Account metadata

L'admin può vedere solo metadata minimi:

- UserId;
- Email;
- DisplayName;
- CreatedAt;
- LastLoginAt;
- IsDisabled;
- AccountStatus;
- CareCircleCount;
- DocumentsCount;
- StorageUsedBytes;
- TimelineEntryCount.

### User operations

L'admin può:

- disabilitare account;
- riabilitare account;
- revocare sessioni utente;
- avviare richiesta cancellazione account/dati.

Ogni azione mutativa richiede una `reason`.

### Audit

- Audit log per ogni azione mutativa.
- Audit log per accessi a log tecnici.
- Audit log per lettura audit log, se richiesto da policy.

### Technical system

- Health checks.
- Log tecnici non sensibili.
- Stato servizi.
- Operazioni recenti.

### Frontend admin

- Login;
- dashboard;
- lista utenti;
- dettaglio utente;
- audit logs;
- operations;
- system health.

## Out of scope

- Lettura timeline;
- lettura titoli timeline;
- lettura contenuti timeline;
- lettura documenti;
- download documenti;
- lettura nomi originali file;
- lettura storage path;
- lettura domande per medici;
- lettura risposte/annotazioni mediche;
- lettura shared updates;
- lettura nomi care circle;
- lettura note private;
- impersonificazione;
- login as user;
- modifica contenuti utente;
- messaging utenti;
- analytics invasive;
- AI admin;
- billing;
- marketplace caregiver;
- gestione clinica;
- triage;
- diagnosi;
- prognosi;
- medical reporting.

## Future scope, non v0.1

Eventuali future funzioni commerciali o hosted possono aggiungere:

- piani/tenant;
- billing metadata;
- quote storage;
- export amministrativi solo su metadata;
- feature flags;
- support access grant con consenso esplicito.

Queste funzioni non sono incluse nella v0.1.
