# Maestro smoke E2E flows

Smoke E2E per Accanto mobile. Maestro è un runner E2E mobile-first di Mobile.dev
che esegue YAML flows contro un device fisico o simulator con l'app già
installata.

## Quando girarli

- **Locale** dopo una build EAS dev/preview, prima di mergere su `main`.
- **Pre-rilascio** su build production firmata, contro account smoke
  configurati lato backend (`SMOKE_EMAIL`, `SMOKE_PASSWORD`).
- **Non in CI** out-of-the-box: richiedono device/simulator → meglio
  lasciarli come gate manuale finché non si imposta un device farm.

## Setup

1. Installare il CLI (richiede Java 11+):
   ```pwsh
   curl -Ls https://get.maestro.mobile.dev | bash
   ```
2. Avviare un emulator Android o simulator iOS e installare l'app
   (`eas build --profile development --platform android --local` poi
   `adb install ...`).
3. Esportare le variabili d'ambiente per il profilo:

   ```pwsh
   $env:MAESTRO_APP_ID = "app.accanto.mobile.dev"
   $env:MAESTRO_SCHEME = "accanto.dev"
   $env:MAESTRO_EMAIL = "smoke@accanto.care"
   $env:MAESTRO_PASSWORD = "***"
   $env:INVITE_TOKEN = "..."
   ```

## Flows

| File                       | Descrizione                                                                  |
| -------------------------- | ---------------------------------------------------------------------------- |
| `login.yaml`               | App start → login → arrivo su Dashboard (`Il tuo spazio`).                   |
| `deeplink-support.yaml`    | Apre `accanto.dev://support` → verifica titolo SupportScreen.                |
| `deeplink-invite.yaml`     | Apre `accanto.dev://invite/<token>` → verifica InviteAcceptScreen.           |

Esegui un singolo flow:

```pwsh
maestro test .maestro/login.yaml
```

Esegui tutti i flow in batch:

```pwsh
maestro test .maestro
```

## Universal link verification

Per testare gli universal link reali (HTTPS) servono prerequisiti che
NON dipendono da Maestro:

- Android: `https://accanto.care/.well-known/assetlinks.json` deve
  contenere il SHA-256 del cert di firma reale + `adb shell pm verify-app-links --re-verify <pkg>`.
- iOS: `https://accanto.care/.well-known/apple-app-site-association`
  deve essere `application/json`, accessibile in chiaro, e l'app deve
  essere installata tramite TestFlight o App Store (le build internal
  non scaricano AASA su prima install).
