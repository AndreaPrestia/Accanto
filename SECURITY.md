# Security Policy

## Versioni supportate

Solo l'ultima release minore (`v0.x`) riceve fix di sicurezza durante lo sviluppo iniziale. Quando il progetto raggiungerà la v1.0 verrà definita una policy di supporto più precisa.

| Versione | Supporto sicurezza |
|----------|--------------------|
| 0.1.x    | ✅ |
| < 0.1    | ❌ |

## Come segnalare una vulnerabilità

**Non aprire una Issue pubblica.**

Usa il canale privato di GitHub:

1. Vai su https://github.com/AndreaPrestia/Accanto/security/advisories/new
2. Compila il report con descrizione, impatto, passi per riprodurre, eventuale proof of concept.
3. Riceverai una risposta entro **7 giorni** con conferma di presa in carico.

In alternativa, se preferisci e-mail, scrivi al maintainer principale (vedi profilo GitHub `AndreaPrestia`) — apri prima una Issue vuota con titolo `security: richiesta di contatto privato` se non hai modo di trovare l'indirizzo, così possiamo coordinarci.

## Cosa aspettarsi

- **Ack entro 7 giorni**.
- **Triage entro 14 giorni**: confermiamo se è una vulnerabilità, ne valutiamo la gravità (CVSS indicativo).
- **Fix**: per vulnerabilità critiche puntiamo a una patch entro 30 giorni. Vulnerabilità a bassa gravità possono entrare nel ciclo normale di release.
- **Disclosure coordinata**: pubblichiamo l'advisory dopo che la patch è rilasciata. Citiamo chi ha segnalato (se lo desidera).

## Ambito

Sono considerate vulnerabilità di sicurezza, fra le altre:

- bypass dell'autenticazione o dell'autorizzazione per cerchio;
- lettura di dati cifrati senza la chiave master;
- accesso a documenti di un altro utente / cerchio;
- escalation di privilegi (Viewer → Caregiver/Owner);
- RCE, SQL injection, XSS persistenti, CSRF su endpoint che modificano dati;
- esposizione di segreti (chiavi, token JWT) nei log o nelle risposte;
- denial of service tramite input non validato.

**Non in scopo (in generale):**

- attacchi che richiedono accesso fisico al server o credenziali admin valide;
- bug nella PWA che non hanno conseguenze di sicurezza (es. cache stale);
- problemi di terze parti (.NET, Postgres, Node) che hanno già un CVE pubblico — segnalali a monte.

## Hardening consigliato a chi fa il deploy

Vedi [README.md → Cifratura a riposo](README.md#cifratura-a-riposo):

- TLS obbligatorio in produzione (Caddy/Traefik davanti al frontend).
- Cifratura del disco (LUKS/BitLocker) sul volume Postgres e sui documenti.
- Backup separati di DB, storage e **chiave master**.
- `Encryption__MasterKey` e `Jwt__Key` in un vault, non in file di testo in chiaro accessibili dal sistema.

## Grazie

A chi prende il tempo di segnalare in modo responsabile: stai proteggendo i caregiver che si fidano di questo strumento per dati delicati.
