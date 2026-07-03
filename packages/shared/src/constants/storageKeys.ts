// Chiavi di persistenza condivise fra il PWA web e l'app React Native.
// Sul web vengono usate come chiavi di localStorage; su mobile come chiavi
// di AsyncStorage / SecureStore (per i token).
export const TOKEN_KEY = 'accanto.token';
export const REFRESH_KEY = 'accanto.refreshToken';
export const USER_KEY = 'accanto.user';
export const LANGUAGE_KEY = 'accanto.language';
