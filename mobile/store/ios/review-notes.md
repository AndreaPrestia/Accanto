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
- **Nota**: l'account demo è pre-popolato con:
  - 1 cerchio di cura "Nonna Maria" con qualche voce di timeline di esempio
  - 1 documento PDF di test
  - 1 domanda per il medico
  - Verifica in due passaggi **disattivata** (per evitare che il reviewer debba
    accedere a un secondo dispositivo/canale)

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

Universal Links: to validate the deep-link flow, open
https://accanto.care/invite/DEMO123 in Safari on a device with the app installed.
The app should open directly without a chooser dialog.

Push notifications: after login, go to Account → Notifiche, tap "Attiva
notifiche" and grant permission. Then trigger a demo notification from the
"Test push" button (visible only for the reviewer account).

Face ID / Touch ID: after login, go to Account → Sicurezza → "Sblocco con
biometria" to enable. On next launch, the app will prompt for biometrics before
showing content.

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
