import AsyncStorage from '@react-native-async-storage/async-storage';

/**
 * Flag "l'utente ha visto il welcome onboarding". Persistito con AsyncStorage.
 * Silenzioso su errori: se AsyncStorage fallisce, l'unica conseguenza è
 * rivedere il welcome al prossimo mount, non un blocco funzionale.
 */
const WELCOME_KEY = 'accanto.hasSeenWelcome';

export async function hasSeenWelcome(): Promise<boolean> {
  try {
    const v = await AsyncStorage.getItem(WELCOME_KEY);
    return v === '1';
  } catch {
    return false;
  }
}

export async function markWelcomeSeen(): Promise<void> {
  try {
    await AsyncStorage.setItem(WELCOME_KEY, '1');
  } catch {
    /* pazienza: al prossimo login riappare */
  }
}
