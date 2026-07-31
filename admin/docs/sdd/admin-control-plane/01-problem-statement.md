# 01 — Problem statement

## Problema

Accanto tratta dati personali e potenzialmente sanitari o emotivamente sensibili.  
Anche se il progetto non è un dispositivo medico e non fornisce diagnosi, può contenere:

- referti caricati dall'utente;
- documenti;
- note personali;
- domande per i medici;
- aggiornamenti a familiari e amici;
- informazioni legate a malattia grave, assistenza, ricovero o lutto.

Se in futuro Accanto viene distribuito come servizio hosted, sarà necessario avere strumenti amministrativi per operazioni tecniche minime.

## Esigenza admin

Serve un control plane per:

- gestire admin users;
- vedere metadata account;
- disabilitare account in caso di abuso o richiesta utente;
- riabilitare account;
- revocare sessioni;
- avviare cancellazione account/dati;
- consultare audit log;
- consultare log tecnici non sensibili;
- verificare stato dei servizi.

## Rischio principale

Un pannello admin tradizionale tende a diventare un super-potere:

- lettura contenuti;
- impersonificazione utenti;
- download documenti;
- query arbitrarie;
- debug invasivo;
- accesso a payload e log completi.

Per Accanto questo sarebbe inaccettabile.

## Decisione di prodotto

Il sistema Admin deve essere progettato come **control plane minimale**.

Gli admin possono gestire la piattaforma, non i contenuti degli utenti.

## Decisione tecnica

Il sistema Admin deve essere separato:

- API separata;
- frontend separato;
- database separato;
- autenticazione separata;
- audit log separato;
- token separati;
- CORS separato;
- dominio admin separato.

## Non-obiettivo

Il sistema Admin non serve a “vedere cosa fanno gli utenti”.  
Non deve servire a supporto clinico.  
Non deve servire ad analytics sui contenuti.  
Non deve servire ad AI, diagnosi, suggerimenti medici o interpretazione documenti.

## Principio guida

> Gli admin gestiscono account, sicurezza e operazioni tecniche. I contenuti appartengono agli utenti.
