# Screenshot — Accanto mobile

Cartella dedicata alle specifiche degli screenshot da caricare su App Store
Connect e Google Play Console. I file `.png` finali **non sono committati** in
questo repo (peserebbero decine di MB e si rigenerano ad ogni release): vanno
prodotti localmente e caricati direttamente nelle console.

## Convenzione file (locale, non committato)

```
screenshots/
├── ios/
│   ├── 6.7-inch/         iPhone 15 Pro Max (1290x2796)         REQUIRED
│   │   ├── 01-dashboard.png
│   │   ├── 02-timeline.png
│   │   ├── 03-documents.png
│   │   ├── 04-doctor-questions.png
│   │   └── 05-self-care.png
│   ├── 6.5-inch/         iPhone 11 Pro Max/XS Max (1284x2778)  REQUIRED (fallback per device 6.5")
│   │   └── ...5 shot corrispondenti
│   └── ipad-13-inch/     (opzionale, se supporti iPad — supportsTablet=true in app.config.ts)
│       └── ...
└── android/
    ├── phone/            min 1080x1920, aspect ratio 16:9 - 9:16  REQUIRED (min 2, max 8)
    │   ├── 01-dashboard.png
    │   └── ...
    └── tablet-7/         opzionale
    └── tablet-10/        opzionale
```

## Dimensioni obbligatorie

### iOS (App Store Connect)

Apple accetta un solo set per "size class" e lo scala automaticamente per gli
altri device dello stesso family. Con il set 6.7" **puoi coprire tutti gli
iPhone moderni**. Aggiungi 6.5" solo se hai artwork ottimizzato per quel
aspect ratio.

| Device                        | Portrait          | Landscape         | Note                    |
|-------------------------------|-------------------|-------------------|-------------------------|
| iPhone 6.7"                   | 1290×2796         | 2796×1290         | **Obbligatorio v1**     |
| iPhone 6.5" (fallback)        | 1284×2778         | 2778×1284         | Opzionale, autoscale    |
| iPad 13" (M4)                 | 2064×2752         | 2752×2064         | Solo se `supportsTablet` |
| iPad 12.9" (fallback)         | 2048×2732         | 2732×2048         | Opzionale               |

### Android (Play Console)

| Device        | Min dimensione     | Aspect ratio      | Numero (min–max) |
|---------------|--------------------|-------------------|------------------|
| Phone         | 1080px lato lungo  | 16:9 → 9:16       | 2–8 (**obbligatorio**) |
| 7" tablet     | 1080px lato lungo  | 16:9 → 9:16       | 1–8 (opzionale)  |
| 10" tablet    | 1080px lato lungo  | 16:9 → 9:16       | 1–8 (opzionale)  |

## Workflow di generazione

### iOS via GitHub Actions macOS (senza Mac locale — raccomandato)

Il workflow `.github/workflows/store-screenshots.yml` gira su runner `macos-15`:

1. Crea un simulator **iPhone 16 Pro Max (6.9")**.
2. Compila l'app con EAS profilo `store-screenshots` (`ios.simulator: true`).
3. Esegue il flow Maestro `.maestro/screenshots.yaml` (login demo → 5 schermate).
4. Carica i PNG raw come artifact `ios-raw-screenshots`.

Setup una tantum (GitHub → Settings → Secrets and variables → Actions):

- secret `EAS_TOKEN` (expo.dev → account → access tokens)
- secret `MAESTRO_EMAIL` + `MAESTRO_PASSWORD` di un account **demo** con dati
  pre-popolati (cerchio `Famiglia Rossi`, timeline, documenti…)

Esecuzione:

```pwsh
# 1. Actions → store-screenshots → Run workflow
# 2. Scarica l'artifact ios-raw-screenshots in:
#    mobile/store/screenshots/_raw/ios/   (file: 01-dashboard.png … 05-self-care.png)
# 3. Componi le canvas App Store (1320x2868 + 1284x2778):
pwsh mobile/scripts/store-screenshots.ps1 -Mode compose-ios
```

Se hai un Mac a disposizione, in alternativa: `xcrun simctl io booted screenshot screen.png`
oppure `Cmd+S` nel simulator.

### Android via emulator (Android Studio)

```pwsh
# 1. Avvia AVD "Pixel 8 Pro" API 34+ (1080x2400)
# 2. Cattura automatica delle 5 schermate + composizione canvas Play (1080x1920):
pwsh mobile/scripts/store-screenshots.ps1 -Mode capture-android
#    (lo script usa `adb exec-out screencap -p` e ti guida schermata per schermata)
```

Cattura automatica (opzionale): esegui prima `maestro test .maestro/screenshots.yaml`
con l'emulatore attivo, poi copia i PNG da `~/.maestro/tests/` in
`store/screenshots/_tmp/` e riadatta.

Alternativa manuale: camera icon nella toolbar dell'emulator, oppure
`adb exec-out screencap -p > screen.png`.

### Ordine e contenuto suggerito (v1)

1. **Dashboard con banner sicurezza + welcome checklist** — mostra che l'app è
   accogliente per il primo accesso.
2. **Timeline di un cerchio** con 3-4 voci esempio (sintomo, cambio terapia,
   nota veloce, upload documento).
3. **Documenti** — griglia con 4-5 documenti campione (ricette, referti).
4. **Domande per il medico** — lista con 3 domande esempio + tag "prossima visita".
5. **Cura di sé / benessere** — schermata check-in con emozione + nota.

**IMPORTANTE**: nessuna informazione reale nei dati del reviewer account. Usare
esclusivamente contenuti campione (es. "Nonna Maria", "Dott. Rossi", sintomi
generici). Nessuna PII di persone reali.

## Text overlay / marketing frames

Opzionale: incorniciare gli screenshot con sfondo brand + titolo/sottotitolo
sopra lo schermo. Ottimo per conversione. **Automatizzato** in questo repo:

```pwsh
# Dopo aver composto i set base (capture-android / compose-ios):
pwsh mobile/scripts/store-screenshots.ps1 -Mode frames -Locales it-IT,en-US,es-ES
```

- Copy e colori in `frames.config.json` (committato, per locale).
- Output in `screenshots/framed/<locale>/<set>/` (gitignored), stesse dimensioni
  del set di partenza → caricabili direttamente nelle console.
- Rendering via `System.Drawing` (nativo Windows, zero dipendenze).

Per la v1 si possono anche caricare screenshot "puri" (senza frame) — Apple e
Google li accettano entrambi.

Alternative esterne: [Screenshots.pro](https://screenshots.pro), [Rotato](https://rotato.app),
[fastlane frameit](https://docs.fastlane.tools/actions/frameit/), o Figma
templates community.

## Verifica prima del submit

```pwsh
# Verifica dimensione esatta:
Get-ChildItem screenshots/ios/6.7-inch/*.png | ForEach-Object {
  $img = [System.Drawing.Image]::FromFile($_.FullName)
  "{0,-40} {1}x{2}" -f $_.Name, $img.Width, $img.Height
  $img.Dispose()
}
```

Se una dimensione è sbagliata, `sips` (macOS) o `sharp` (script Node) permette
ridimensionamento senza perdita di qualità visiva significativa.
