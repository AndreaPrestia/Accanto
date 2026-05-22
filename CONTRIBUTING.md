# Contribuire ad Accanto

Grazie per essere qui. Accanto è un progetto piccolo, gestito principalmente da una persona, con l'aiuto di un agente AI (Claude). Ogni contributo, anche minuscolo, è benvenuto: segnalare un refuso, raccontare un caso d'uso, proporre una traduzione, scrivere codice.

## Codice di condotta

In sintesi: **siamo qui per servire chi sta accudendo una persona cara**. Discussioni rispettose, attenzione al tono, zero linguaggio aggressivo. Chi non rispetta queste regole viene rimosso.

## Modi per contribuire (in ordine di "facilità")

1. **Provare Accanto e raccontare** — apri una Discussion con "ho provato Accanto per X giorni, ho notato Y". Per chi scrive il codice è il feedback più prezioso che esista.
2. **Segnalare un bug** — apri una Issue usando il template Bug. Includi passi per riprodurre, cosa ti aspettavi, cosa è successo, versione/commit.
3. **Proporre una feature** — apri una Issue usando il template Feature. Spiega prima il **bisogno** (chi sei, cosa stavi cercando di fare), poi la soluzione che hai in mente. Le proposte che partono dal bisogno hanno priorità.
4. **Migliorare la copy italiana** — alcune frasi possono essere più gentili, meno tecniche, più chiare. Apri una PR con il testo nuovo.
5. **Scrivere codice** — vedi la sezione seguente.

## Setup di sviluppo

Vedi [README.md → Sviluppo locale](README.md#sviluppo-locale-senza-docker). In breve: .NET 10 SDK, Node 22, PostgreSQL 16 (o usa Docker per il solo DB).

## Pull request

1. **Apri prima una Issue** se il cambiamento è più di poche righe — evita di lavorare a vuoto se la direzione non è condivisa.
2. **Branch dal `main`**, nome breve descrittivo (`feat/invite-link`, `fix/timeline-date-tz`).
3. **Commit Conventional**: `feat(timeline): filtro per range data`, `fix(documents): decifratura su download fallisce con file > 5MB`, `docs(readme): aggiungi sezione TLS`. Tipi accettati: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `ci`, `perf`.
4. **Test**: se tocchi il backend, aggiungi/aggiorna i test in `backend/tests/Accanto.Tests/`. La CI gira `dotnet test` su tutto.
5. **Niente segreti committati**: mai chiavi, password, dump di DB reali. Il `.env` è gitignorato per un motivo.
6. **Una PR = un cambiamento**: meglio tre PR piccole che una gigante.

## Cosa non viene accettato

- Telemetria, analytics, tracker di qualsiasi tipo.
- Dipendenze da servizi SaaS esterni non opzionali (e-mail di sistema, file storage cloud, AI mandatoria).
- Cambi di licenza diversi da AGPL-3.0.
- Feature "smart" che inviano dati medici a terzi senza consenso esplicito dell'utente finale.

## Privacy e dati sensibili nei bug report

Se per riprodurre un bug servono dati di esempio, **inventali**. Mai allegare contenuti reali di diario, documenti veri, nomi di persone assistite. Se devi mostrare uno screenshot, oscura i dati.

## Sicurezza

Per vulnerabilità di sicurezza **non aprire una Issue pubblica**: leggi [SECURITY.md](SECURITY.md).

## Licenza dei contributi

Aprendo una PR accetti che il tuo contributo venga rilasciato sotto **AGPL-3.0**, la stessa licenza del progetto.
