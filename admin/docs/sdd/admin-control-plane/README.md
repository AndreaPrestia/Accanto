# Accanto Admin Control Plane — SDD Documentation

Questo pacchetto contiene la documentazione SDD completa per sviluppare il sistema Admin separato di Accanto.

## Obiettivo

Aggiungere ad Accanto un sistema Admin separato composto da:

- Admin API separata;
- Admin Frontend separato;
- Admin DB separato;
- autenticazione admin separata;
- audit log admin;
- operazioni tecniche minime sugli utenti;
- zero accesso ai contenuti utente.

## Principio guida

> Gli admin gestiscono la piattaforma, non leggono la vita privata degli utenti.

Accanto può contenere appunti, documenti, referti, domande mediche, aggiornamenti familiari e contenuti emotivamente o clinicamente sensibili. Il sistema Admin deve essere progettato per non poterli leggere.

## Struttura

```text
docs/sdd/admin-control-plane/
  00-context.md
  01-problem-statement.md
  02-scope.md
  03-privacy-boundary.md
  04-architecture.md
  05-api-spec.md
  06-admin-frontend-spec.md
  07-data-model.md
  08-security-model.md
  09-implementation-plan.md
  10-test-plan.md
  11-acceptance-checklist.md
  12-agent-prompts.md
  final-verification-report-template.md
  adr/
    ADR-0001-admin-control-plane.md
    ADR-0002-admin-db-separation.md
    ADR-0003-no-content-access.md
    ADR-0004-service-to-service-boundary.md
  tasks/
    TASK-001-repository-survey.md
    TASK-002-admin-domain.md
    TASK-003-admin-db.md
    TASK-004-admin-auth.md
    TASK-005-internal-user-metadata.md
    TASK-006-admin-user-operations.md
    TASK-007-admin-audit-log.md
    TASK-008-admin-frontend.md
    TASK-009-docker-compose.md
    TASK-010-tests.md
    TASK-011-docs.md
```

## Come usarla con Claude/Kimi

1. Copia la cartella `docs/sdd/admin-control-plane/` nel repository.
2. Dai al coding agent il file `12-agent-prompts.md`.
3. Fai eseguire una fase alla volta.
4. Non chiedere mai “implementa tutto” in un unico prompt.
5. Dopo ogni task, fai build/test e verifica la privacy boundary.

## Vincoli non negoziabili

- Non aggiungere `User.IsAdmin`.
- Non salvare gli admin nella tabella utenti pubblica.
- Non aggiungere route admin dentro la PWA pubblica.
- Non leggere timeline, documenti, domande, aggiornamenti o note degli utenti.
- Non mostrare nomi care circle.
- Non mostrare nomi originali dei file.
- Non implementare impersonificazione.
- Non implementare hard delete immediata dal pannello admin.
- Ogni azione mutativa admin deve richiedere una `reason`.
- Ogni azione mutativa admin deve scrivere audit log.
