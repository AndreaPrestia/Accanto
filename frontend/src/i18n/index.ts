import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { it, en, es } from '@accanto/shared/i18n/locales';
import {
  SUPPORTED_LANGUAGES as SHARED_SUPPORTED_LANGUAGES,
  LANGUAGE_LABEL as SHARED_LANGUAGE_LABEL,
  DEFAULT_LANGUAGE
} from '@accanto/shared/i18n/constants';
import type { SupportedLanguage as SharedSupportedLanguage } from '@accanto/shared/i18n/constants';
import { LANGUAGE_KEY } from '@accanto/shared/constants/storageKeys';

// Re-export per backward compatibility con il resto del frontend
// (es. AuthProvider, LanguageSwitcher che usano SUPPORTED_LANGUAGES e LANGUAGE_LABEL).
export const SUPPORTED_LANGUAGES = SHARED_SUPPORTED_LANGUAGES;
export type SupportedLanguage = SharedSupportedLanguage;
export const LANGUAGE_LABEL = SHARED_LANGUAGE_LABEL;

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      it: { translation: it },
      en: { translation: en },
      es: { translation: es }
    },
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: SUPPORTED_LANGUAGES as unknown as string[],
    nonExplicitSupportedLngs: true,
    interpolation: { escapeValue: false },
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: LANGUAGE_KEY,
      caches: ['localStorage']
    }
  });

export default i18n;
