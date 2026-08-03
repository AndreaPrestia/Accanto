# App Review Information — Accanto iOS

Copiare i campi in App Store Connect → App Information → App Review Information
al momento della prima submission (e ad ogni resubmit di build significativa).

## Sign-in required

**Yes** (l'app richiede login per accedere a qualsiasi funzionalità).

## Demo account

- **Email**: `reviewer@accanto.care`
- **Password**: da generare in produzione **prima del submit** e inserire qui.
  Non committare la password in questo file. Usa password random 16+ char,
  cambiala dopo ogni ciclo di review.
- **Nota**: l'account demo è pre-popolato con (verificato via API su prod, 2026-08-03):
  - 1 cerchio di cura **"Famiglia Rossi"** (assistenza alla nonna Maria), status
    Active, ruolo Owner — con diverse voci di timeline (nota personale,
    appuntamento, sintomo, terapia)
  - documenti PDF di test (referto, esami, ricetta, imaging, dieta — 1 pagina, zero PII)
  - domande per il medico (stati: da chiedere / chiesta / risposta ricevuta)
  - Verifica in due passaggi **disattivata** (per evitare che il reviewer debba
    accedere a un secondo dispositivo/canale); deadline 2FA Owner estesa così il
    middleware non blocca l'account durante la review

## Contact information

- **Nome**: Andrea Prestia (referente tecnico)
- **Email**: `support@accanto.care`
- **Phone**: (da compilare al submit, richiesto da Apple)

## Notes for the reviewer

```
Accanto is a coordination platform for family caregivers assisting a relative.
It is NOT a medical device, does NOT provide diagnoses, treatments, medical
advice, or any form of health monitoring. All content (notes, documents, questions)
is user-generated free-form text or uploaded files — the app does not analyse
or interpret it.

Navigation: open the drawer (top-left menu "Apri menu"). "Le mie care circle"
is the home. Tap the "Famiglia Rossi" card to open a care circle, then use the
bottom tabs: Panoramica (overview), Diario (timeline), Documenti (documents),
Domande (doctor questions), Aggiornamenti (shared updates). The drawer also has
"Prenditi cura di te" (self-care content for the caregiver).

Push notifications: after login, open the drawer → Account → "Sicurezza"
section → "Dispositivi push" / "Preferenze notifiche". Granting permission
registers the device; notifications are triggered by real events (e.g. a new
timeline entry from another member). There is no synthetic "test" trigger.

Universal Links: the app declares associatedDomains applinks:accanto.care.
Invite links have the form https://accanto.care/invite/<token>; open one in
Safari on a device with the app installed and it opens the in-app invite flow.
(No pre-generated public demo token is shipped; the flow can be exercised by
sending a real invite from the demo circle → InvitesPanel.)

Data controller: PRESTIA.DEV S.A.S., Rome, Italy. Full privacy policy:
https://accanto.care/en/privacy

No third-party analytics. No advertising. AI features are optional, disabled by
default, and run on a self-hosted Ollama instance (no data leaves our EU
infrastructure).

For any question during review, contact support@accanto.care — we typically
reply within 24 hours (business days).
```

## Attachments

Nessuno obbligatorio. Se il reviewer chiede screenshot del flusso, allegare
gli stessi PNG usati per gli screenshot store (vedi `../screenshots/`).
