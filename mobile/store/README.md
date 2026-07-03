# Store listings — Accanto mobile

Metadata testuali e riferimenti per la pubblicazione su App Store e Google Play.
Ogni file è pensato per essere copiato direttamente nella console corrispondente
(App Store Connect / Play Console) senza rielaborazione.

## Struttura

```
store/
├── ios/                        App Store Connect
│   ├── it-IT/  en-US/  es-ES/  Metadata per locale
│   │   ├── name.txt              (max 30 char)  → App Information → Name
│   │   ├── subtitle.txt          (max 30)       → Subtitle
│   │   ├── promotional-text.txt  (max 170)      → Promotional Text (editabile senza review)
│   │   ├── description.txt       (max 4000)     → Description
│   │   ├── keywords.txt          (max 100)      → Keywords (comma separated, no spaces)
│   │   └── release-notes.txt     (max 4000)     → What's New in This Version
│   ├── review-notes.md         → App Review Information (test account + note reviewer)
│   └── privacy-nutrition.md    → App Privacy questionnaire (Data Types)
│
├── android/                    Google Play Console
│   ├── it-IT/  en-US/  es-ES/
│   │   ├── title.txt             (max 30)       → Store listing → App name
│   │   ├── short-description.txt (max 80)       → Short description
│   │   ├── full-description.txt  (max 4000)     → Full description
│   │   └── release-notes.txt     (max 500)      → What's new
│   ├── data-safety.md          → Data safety form (equivalente Play della App Privacy iOS)
│   └── content-rating.md       → IARC questionnaire
│
└── screenshots/                Screenshot specifiche + workflow (i file .png/.jpg
                                sono generati via Expo/simulator, NON committati:
                                pesano troppo e si rigenerano ad ogni release)
```

## Lingua primaria

**IT è la lingua di riferimento.** Ogni modifica di copy va prima in `it-IT/`,
poi propagata in `en-US/` (obbligatoria per iOS US market) e `es-ES/` (mercato
secondario). Se una lingua non è compilata, gli store mostrano IT come fallback.

## Vincoli di lunghezza (verifica prima del submit)

```pwsh
Get-ChildItem mobile/store -Recurse -Filter *.txt | ForEach-Object {
  $len = (Get-Content $_.FullName -Raw).TrimEnd().Length
  "{0,-60} {1,4} char" -f $_.FullName.Replace((Get-Location).Path + "\", ""), $len
}
```

Confronta col commento in cima al file `README.md` di ogni gruppo (soft-limit del rispettivo store).

## Update workflow ad ogni release

1. Bump versione in `mobile/app.config.ts` (semver) e `eas build --auto-submit`
2. Aggiorna `ios/*/release-notes.txt` e `android/*/release-notes.txt` (in tutte le locali attive)
3. Se cambia funzionalità visibile: aggiorna `description.txt` / `full-description.txt`
4. Se cambiano dati raccolti: aggiorna `privacy-nutrition.md` e `data-safety.md` **prima** del submit
5. Copia i contenuti aggiornati in ASC / Play Console → salva → submit build

## Screenshot

Vedi `screenshots/README.md` per dimensioni obbligatorie per device e workflow di
generazione via simulator iOS + Android Studio emulator.
