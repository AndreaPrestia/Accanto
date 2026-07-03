import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Localization from 'expo-localization';
import { it, en, es } from '@accanto/shared/i18n/locales';
import {
  SUPPORTED_LANGUAGES,
  DEFAULT_LANGUAGE
} from '@accanto/shared/i18n/constants';
import { LANGUAGE_KEY } from '@accanto/shared/constants/storageKeys';

export {
  SUPPORTED_LANGUAGES,
  LANGUAGE_LABEL,
  DEFAULT_LANGUAGE
} from '@accanto/shared/i18n/constants';
export type { SupportedLanguage } from '@accanto/shared/i18n/constants';

const supported = SUPPORTED_LANGUAGES as readonly string[];

// Detector custom: legge la preferenza esplicita da AsyncStorage; se assente
// usa la lingua di sistema (expo-localization); fallback DEFAULT_LANGUAGE.
async function detectInitialLanguage(): Promise<string> {
  try {
    const stored = await AsyncStorage.getItem(LANGUAGE_KEY);
    if (stored && supported.includes(stored)) return stored;
  } catch {
    // ignore — fallback alla lingua di sistema
  }
  const locales = Localization.getLocales();
  for (const loc of locales) {
    const lang = (loc.languageCode ?? '').toLowerCase();
    if (lang && supported.includes(lang)) return lang;
  }
  return DEFAULT_LANGUAGE;
}

export async function initI18n(): Promise<typeof i18n> {
  if (i18n.isInitialized) return i18n;
  const lng = await detectInitialLanguage();
  await i18n.use(initReactI18next).init({
    compatibilityJSON: 'v4',
    resources: {
      it: { translation: it },
      en: { translation: en },
      es: { translation: es }
    },
    lng,
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: supported,
    nonExplicitSupportedLngs: true,
    interpolation: { escapeValue: false }
  });
  return i18n;
}

// Persist + apply language change. Called from AccountScreen / LanguageSwitcher.
export async function persistLanguage(lng: string): Promise<void> {
  if (!supported.includes(lng)) return;
  try {
    await AsyncStorage.setItem(LANGUAGE_KEY, lng);
  } catch {
    // ignore: la lingua viene comunque applicata in-memory
  }
  await i18n.changeLanguage(lng);
}

export default i18n;
