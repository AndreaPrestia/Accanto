# Accanto Mobile (React Native + Expo)

Versione React Native dell'app Accanto, in feature parity con il [PWA web](../frontend/). Stack:

- **Expo SDK 53** (managed) + EAS Build per le build di produzione (iOS + Android).
- **React Navigation** (stack + drawer + bottom tab).
- **NativeWind** per riusare la sintassi Tailwind condivisa con il web.
- **expo-secure-store** per i token JWT, **AsyncStorage** per user + lingua.
- **Expo Notifications** per le push native (richiede il canale Device Push lato backend).
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
npx expo start --tunnel    # esegue su device fisico via Expo Go
```

## API base URL

Per default `app.config.ts` legge `EXPO_PUBLIC_API_BASE_URL` da env. In sviluppo:

```pwsh
$env:EXPO_PUBLIC_API_BASE_URL = "http://192.168.1.50:5170/api"
npx expo start
```

## Stato implementazione

Vedi piano in `/memories/session/plan.md`. Phase corrente: bootstrap (i18n + theme + entry).
