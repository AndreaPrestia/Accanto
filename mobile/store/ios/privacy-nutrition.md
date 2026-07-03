# App Privacy — Accanto iOS (App Store Connect questionnaire)

Compilare in App Store Connect → App Privacy prima del primo submit. Tenere
sincronizzato con `../../../web/src/pages/it/privacy.astro` (fonte di verità legale).

## Data Types Collected

### Linked to the user (identity-linked)

| Data Type              | Category           | Purpose               |
|------------------------|--------------------|-----------------------|
| Email Address          | Contact Info       | App Functionality     |
| Name                   | Contact Info       | App Functionality     |
| Other User Content     | User Content       | App Functionality     |
| Photos or Videos       | User Content       | App Functionality (solo se l'utente allega documenti immagine) |
| Other Diagnostic Data  | Diagnostics        | App Functionality (log tecnici lato server, cancellabili con account) |
| User ID                | Identifiers        | App Functionality (UUID interno) |

### NOT linked to the user

Nessuno.

### Data Used to Track You

**None.** Accanto non fa tracking cross-app o cross-site, non usa SDK pubblicitari
o di analytics, non condivide dati con data broker.

## Purposes (per ogni data type sopra)

- **App Functionality**: ✅ Yes (unica finalità dichiarata)
- Analytics: ❌ No
- Product Personalization: ❌ No
- App Functionality: ✅ (unica selezionata)
- Third-Party Advertising: ❌ No
- Developer's Advertising or Marketing: ❌ No
- Other Purposes: ❌ No

## Data Collection UI (Privacy Nutrition Label)

Con la configurazione sopra, la label mostrata su App Store sarà:

> **Data Linked to You**
> - Contact Info (Email address, Name)
> - User Content (Other user content, Photos or videos)
> - Identifiers (User ID)
> - Diagnostics (Other diagnostic data)
>
> **Data Not Linked to You**: None
> **Data Used to Track You**: None

## Sub-processors (per la privacy policy, non entrano nella label ASC)

- **Apple APNs** (push notifications) — riceve solo push token opaco
- **Google FCM** (push Android — non usato su iOS)
- **IONOS** (hosting UE, Germania) — dati cifrati a riposo
- **Expo/EAS** (build service, non runtime) — non accede a dati utente
- **Nessun servizio AI cloud** (Ollama self-hosted)

## Manutenzione

Aggiornare questo file **prima** del submit ogni volta che:
- Si aggiunge una nuova categoria di dati raccolti (es. localizzazione, contatti device)
- Si integra un nuovo SDK di terze parti
- Cambia la finalità di uso di un dato esistente
- Cambia un sub-processor
