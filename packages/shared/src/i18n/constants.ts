export const SUPPORTED_LANGUAGES = ['it', 'en', 'es'] as const;
export type SupportedLanguage = typeof SUPPORTED_LANGUAGES[number];

export const LANGUAGE_LABEL: Record<SupportedLanguage, string> = {
  it: 'Italiano',
  en: 'English',
  es: 'Español'
};

export const DEFAULT_LANGUAGE: SupportedLanguage = 'it';
