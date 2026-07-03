# Content rating — Accanto Android (IARC questionnaire)

Compilare in Play Console → App content → Content rating con le risposte
seguenti. L'esito atteso è **Everyone / PEGI 3 / USK 0 / ClassInd L**.

Per iOS l'equivalente è il questionario "Age Rating" in App Store Connect →
esito atteso **4+**.

## Category

**Utility, Productivity, Communication, or Other**

Selezionare: **Utility / Productivity / Reference / Other** (non è social, non
è gioco, non è entertainment).

## Answers

| Question                                                     | Answer  | Notes |
|--------------------------------------------------------------|---------|-------|
| Violence — realistic or cartoon                              | **No**  | Nessun contenuto violento. |
| Sexual content or nudity                                     | **No**  | Nessuno. |
| Profanity or crude humor                                     | **No**  | Nessuno. |
| Controlled substances (alcohol, tobacco, drugs)              | **No**  | Nessun riferimento promozionale. Le note dell'utente possono menzionare farmaci prescritti, ma è contenuto libero, non promozione. |
| Simulated gambling                                           | **No**  | Assente. |
| Real gambling                                                | **No**  | Assente. |
| User-generated content shared with other users              | **Yes** | Le note di timeline, i documenti e le domande sono condivisi **solo** con i membri del cerchio, tutti invitati esplicitamente dall'Owner. Nessuna condivisione pubblica. |
| Users can interact                                           | **Yes** | Solo tra membri di un cerchio privato invitati. Nessuna chat pubblica, nessuna community aperta, nessun matching casuale. |
| Users can share personal info (name, email, phone, address) | **Yes** | Volontario, all'interno di cerchi privati. L'app non forza né incentiva la condivisione di dati sensibili. |
| Users can share their physical location                      | **No**  | Nessuna funzione di geolocalizzazione. |
| Uses digital purchases                                       | **No**  | Nessun IAP. App gratuita (self-hosted) o abbonamento futuro gestito lato server, non tramite billing store. |
| Contains ads                                                 | **No**  | Nessuna pubblicità. |

## Additional details for the reviewer

```
Accanto is a private coordination tool for family caregivers. All content
is user-generated within private "care circles" invited explicitly by the
owner (typically a family member). There is no public feed, no discovery,
no matching, no chat with strangers. All interactions are between people
who already know each other in real life.

No third-party analytics, no advertising SDK, no in-app purchases via
store billing.
```

## Manutenzione

Ricompilare il questionario se:
- Si aggiunge una nuova funzionalità che tocca uno dei domini sopra
- Si introducono acquisti in-app tramite Play Billing
- Google Play aggiorna il questionario (~1 volta/anno)
