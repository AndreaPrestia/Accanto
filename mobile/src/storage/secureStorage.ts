import * as SecureStore from 'expo-secure-store';
import {
  TOKEN_KEY,
  REFRESH_KEY
} from '@accanto/shared/constants/storageKeys';

// expo-secure-store: cifrato via Keychain (iOS) / Keystore (Android).
// Limite di ~2 KB per chiave su Android; un JWT JWS firmato HS256 con claim
// minimi è sotto questo limite (~600–900 byte). Se in futuro i token crescono,
// fallback: salva in AsyncStorage cifrato con chiave da SecureStore.

const OPTIONS: SecureStore.SecureStoreOptions = {
  keychainAccessible: SecureStore.WHEN_UNLOCKED
};

export async function getToken(): Promise<string | null> {
  return SecureStore.getItemAsync(TOKEN_KEY, OPTIONS);
}

export async function setToken(value: string): Promise<void> {
  await SecureStore.setItemAsync(TOKEN_KEY, value, OPTIONS);
}

export async function clearToken(): Promise<void> {
  await SecureStore.deleteItemAsync(TOKEN_KEY, OPTIONS);
}

export async function getRefreshToken(): Promise<string | null> {
  return SecureStore.getItemAsync(REFRESH_KEY, OPTIONS);
}

export async function setRefreshToken(value: string): Promise<void> {
  await SecureStore.setItemAsync(REFRESH_KEY, value, OPTIONS);
}

export async function clearRefreshToken(): Promise<void> {
  await SecureStore.deleteItemAsync(REFRESH_KEY, OPTIONS);
}

// Helper opaco per altre chiavi “secret-grade” che potrebbero servire in futuro
// (es. token push, chiavi di cifratura at-rest).
export async function getSecret(key: string): Promise<string | null> {
  return SecureStore.getItemAsync(key, OPTIONS);
}

export async function setSecret(key: string, value: string): Promise<void> {
  await SecureStore.setItemAsync(key, value, OPTIONS);
}

export async function clearSecret(key: string): Promise<void> {
  await SecureStore.deleteItemAsync(key, OPTIONS);
}
