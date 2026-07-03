# Data safety — Accanto Android (Google Play Console)

Compilare in Play Console → App content → Data safety prima del primo submit.
Tenere sincronizzato con `../ios/privacy-nutrition.md` e con
`../../../web/src/pages/it/privacy.astro` (fonte di verità legale).

## Data collection and security

- **Is all data collected encrypted in transit?** ✅ Yes (HTTPS/TLS 1.3 obbligatorio)
- **Do you provide a way for users to request that their data is deleted?** ✅ Yes
  (Account → Elimina account → cancellazione irreversibile entro 30 giorni;
  contatto diretto `support@accanto.care` per assistenza).

## Data types collected

Per ogni tipo indicare: Collected (yes/no), Shared (yes/no), Processing (ephemeral
or persistent), Optional (yes/no), Purpose(s).

| Data type                     | Collected | Shared | Purpose                     | Optional |
|-------------------------------|-----------|--------|-----------------------------|----------|
| Email address                 | Yes       | No     | Account management          | No       |
| Name                          | Yes       | No     | Account management, App functionality | No |
| User IDs                      | Yes       | No     | Account management          | No       |
| Photos                        | Yes       | No     | App functionality (upload documenti immagine, opzionale) | Yes |
| Files and docs                | Yes       | No     | App functionality (upload documenti clinici) | Yes |
| Other user-generated content  | Yes       | No     | App functionality (note timeline, domande medico) | No |
| Diagnostic data (crash logs)  | No        | —      | —                           | —        |
| Device or other IDs           | No        | —      | —                           | —        |
| Approximate/precise location  | No        | —      | —                           | —        |
| Health and fitness            | No*       | —      | —                           | —        |

\* Le note di timeline possono contenere informazioni sulla salute della persona
assistita, ma sono **contenuto libero generato dall'utente**, non dati sanitari
strutturati né provenienti da sensori/wearable. Play Console distingue le due
cose: dichiariamo "Other user-generated content", non "Health and fitness".

## Data sharing

**No data is shared with third parties for advertising, analytics, or
personalization.** Gli unici sub-processor necessari al funzionamento tecnico
sono:

- **Google FCM** (push notifications) — riceve solo token push opachi + payload
  cifrato lato server. Non è "sharing" ai fini della Data safety Play, ma va
  documentato nella privacy policy.
- **IONOS** (hosting UE) — dati cifrati a riposo.
- **Nessun SDK di analytics, advertising, crash reporting terzo.**

## Security practices

- ✅ Data is encrypted in transit
- ✅ You can request that data is deleted
- ✅ Follows [Families Policy](https://support.google.com/googleplay/android-developer/answer/9893335)
  (l'app non è target minori, ma non contiene contenuti inappropriati)
- ✅ Committed to [Play Families Policy](https://support.google.com/googleplay/android-developer/answer/9866839)
  se applicabile

## Independent security review

Nessuna review indipendente ancora eseguita. Da valutare per la v2.0 (Cure53,
Radically Open Security, o simile) — segnare in `../../accanto-ops/mobile-release-plan.md`.

## Manutenzione

Aggiornare **prima** del submit ogni volta che:
- Si aggiunge una nuova categoria di dati raccolti
- Si integra un nuovo SDK
- Cambia una finalità di uso
- Cambia un sub-processor
- Google Play aggiunge nuovi campi al questionario (avviene ~1 volta/anno)
