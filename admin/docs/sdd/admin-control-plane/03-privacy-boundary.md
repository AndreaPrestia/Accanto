# 03 — Privacy boundary

## Principio

Il sistema Admin deve minimizzare l'accesso ai dati.

L'admin deve poter gestire la piattaforma, non leggere contenuti utente.

## Data access matrix

| Data | Admin can access? | Notes |
|---|---:|---|
| UserId | Yes | Metadata tecnico necessario |
| User email | Yes | Necessario per gestione account |
| User display name | Yes | Metadata account |
| CreatedAt | Yes | Metadata account |
| LastLoginAt | Yes | Metadata account |
| IsDisabled | Yes | Stato account |
| AccountStatus | Yes | Stato account |
| CareCircleCount | Yes | Aggregato |
| CareCircle.Name | No | Potenzialmente sensibile |
| CareCircle.Description | No | Potenzialmente sensibile |
| TimelineEntry count | Yes | Aggregato |
| TimelineEntry.Type aggregate | Maybe | Solo se non rivela contenuto; evitare in v0.1 |
| TimelineEntry.Title | No | Sensibile |
| TimelineEntry.Content | No | Sensibile |
| TimelineEntry.Tags | No | Sensibile |
| MedicalDocument count | Yes | Aggregato |
| MedicalDocument.SizeInBytes aggregate | Yes | Aggregato |
| MedicalDocument.OriginalFileName | No | Sensibile |
| MedicalDocument.StoragePath | No | Sensibile/security-sensitive |
| MedicalDocument.Notes | No | Sensibile |
| MedicalDocument.Tags | No | Sensibile |
| File content | No | Vietato |
| DoctorQuestion count | Maybe | Aggregato; evitare se non serve |
| DoctorQuestion.Question | No | Sensibile |
| DoctorQuestion.AnswerNotes | No | Sensibile |
| SharedUpdate count | Maybe | Aggregato; evitare se non serve |
| SharedUpdate.Content | No | Sensibile |
| Private notes | No | Vietato |
| Audit log admin | Yes | Contiene solo metadata admin |
| Technical logs | Yes, restricted | Solo senza payload sensibili |
| Request/response body logs | No | Vietato |
| Error stacktrace in production | No | Vietato |

## Sensitive examples

Non mostrare mai dati come:

```text
Mamma
Papà ricovero
TAC_mamma_metastasi.pdf
Domani chiedere al medico della morfina
Aggiornamento per parenti
Ha smesso di mangiare
```

Anche se sembrano “solo testi”, sono contenuti molto sensibili.

## DTO rule

Ogni DTO admin deve essere controllato con questa domanda:

> Questo campo aiuterebbe un admin a leggere la situazione clinica, familiare o emotiva dell'utente?

Se sì, il campo è vietato.

## UI rule

Non basta nascondere in UI.  
I dati vietati non devono arrivare al frontend admin.

## API rule

Non basta dire “l'admin non clicca quel bottone”.  
Non devono esistere endpoint admin per leggere contenuti.

## Database rule

Il database admin non deve contenere copie di contenuti sensibili.

## Logging rule

I log admin e tecnici non devono contenere payload utente, body request/response, original filenames o contenuti testuali.
