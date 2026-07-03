import it from './locales/it.json';
import en from './locales/en.json';
import es from './locales/es.json';
import type { SupportedLanguage } from './constants';

export const locales: Record<SupportedLanguage, Record<string, unknown>> = {
  it,
  en,
  es
};

export { it, en, es };
