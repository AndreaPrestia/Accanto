# Accanto Mobile (React Native + Expo)

Versione React Native dell'app Accanto, in feature parity con la [PWA web](../frontend/). Stack:

- **Expo SDK 53** (managed) + EAS Build per le build firmate (iOS + Android).
- **React Navigation** (stack + drawer + bottom tab).
- **NativeWind** per riusare la sintassi Tailwind condivisa con il web.
- **expo-secure-store** per i token JWT, **AsyncStorage** per user + lingua.
- **expo-notifications** per le push native (Expo Push Service, vedi `src/lib/push.ts`).
- **expo-local-authentication** per l'unlock biometrico (opt-in dalle Impostazioni).
- Codice condiviso col PWA via `@accanto/shared` (tipi, i18n locales, costanti di storage, dati statici).

## Setup

Dalla root del monorepo:

```pwsh
npm install
```

Quindi:

```pwsh
cd mobile
npx expo install --check   # verifica versioni SDK
npx expo start --tunnel    # esegue su device fisico via Expo Go o Dev Client
```

## API base URL in dev

Per default `app.config.ts` legge `EXPO_PUBLIC_API_BASE_URL` da env. In sviluppo locale:

```pwsh
$env:EXPO_PUBLIC_API_BASE_URL = "http://192.168.1.50:5170/api"
npx expo start
```

## EAS Build — profili

`eas.json` definisce tre profili allineati al deployment del backend:

| Profilo       | Distribuzione | Bundle id                   | App name             | API target                          |
| ------------- | ------------- | --------------------------- | -------------------- | ----------------------------------- |
| `development` | internal      | `app.accanto.mobile.dev`    | `Accanto (Dev)`      | `https://api.dev.accanto.care`      |
| `preview`     | internal      | `app.accanto.mobile.preview`| `Accanto (Preview)`  | `https://api.staging.accanto.care`  |
| `production`  | store         | `app.accanto.mobile`        | `Accanto`            | `https://api.accanto.care`          |

`APP_VARIANT` viene impostato dal profilo EAS (vedi `app.config.ts`) e seleziona
bundle id, suffisso URL scheme (`accanto.dev://`, `accanto.preview://`, `accanto://`)
e label. I tre variant possono convivere sullo stesso device.

Comandi:

```pwsh
# Login una volta sola (richiede account expo.dev)
npx eas login

# Configurazione iniziale del progetto (genera EAS_PROJECT_ID)
npx eas init

# Build internal (APK Android + IPA distribuzione interna)
npx eas build --profile development --platform all
npx eas build --profile preview --platform all

# Build production (App Bundle + IPA firmati per gli store)
npx eas build --profile production --platform all
```

## Universal / App Links

Le build production usano `https://accanto.care/...` come universal link.
Il dominio serve due file statici (da `web/public/.well-known/`):

- `apple-app-site-association` — content-type `application/json`, deve includere il
  `<TeamID>.app.accanto.mobile` reale. Placeholder `REPLACE_WITH_APPLE_TEAM_ID` da
  sostituire dopo aver configurato l'App ID su Apple Developer.
- `assetlinks.json` — content-type `application/json`, deve includere il SHA-256 del
  certificato di firma release Android (`eas credentials` per estrarlo). Placeholder
  `REPLACE_WITH_RELEASE_KEYSTORE_SHA256_FINGERPRINT` da sostituire alla prima build.

`web/nginx.conf` ha già `location =` espliciti per entrambi che forzano
`Content-Type: application/json` e disabilitano la cache.

Per verificare l'AASA dopo il deploy:

```pwsh
curl -sI https://accanto.care/.well-known/apple-app-site-association
# Atteso: Content-Type: application/json
# Apple validator: https://branch.io/resources/aasa-validator/
```

Per verificare assetlinks:

```pwsh
curl -s https://accanto.care/.well-known/assetlinks.json | jq .
# Validator Google: https://developers.google.com/digital-asset-links/tools/generator
```

Universal Links iOS funzionano solo dopo che l'app è stata installata (anche da TestFlight) e l'utente
ha aperto una volta l'app — è iOS che scarica AASA on demand, non la prima build.

## Push notifications (P6)

`expo-notifications` + Expo Push Service:

- `src/lib/push.ts`: registra il device dopo login, deregistra al logout.
- Backend: tabella `device_push_tokens` + endpoint `/api/account/push-devices`
  (POST/GET/DELETE). Vedi `Accanto.Application/Push/` e `Accanto.Infrastructure/Push/`.
- Foreground handler globale in `App.tsx`.
- Tap handler in `RootNavigator.tsx`: legge `data.circleId` e apre il `Circle` screen.

Per testare in dev (Expo Go non riceve push reali — serve un dev client):

```pwsh
npx eas build --profile development --platform ios
# Installa l'IPA risultante, fai login, controlla che il device appaia
# in /api/account/push-devices.
```

## Stato implementazione

Phase 1-7 complete (auth, navigazione, dashboard, timeline, documenti, doctor questions,
shared updates, account/sessions/2FA/audit/check-in, AI assist, support, self-care,
push notifications, EAS profiles, AASA/assetlinks). Phase 8: test e Maestro E2E.
