import it from './it.json';
import en from './en.json';
import es from './es.json';

export const LOCALES = ['it', 'en', 'es'] as const;
export type Locale = (typeof LOCALES)[number];
export const DEFAULT_LOCALE: Locale = 'it';

const dictionaries = { it, en, es } as const;

type Dictionary = typeof it;

/**
 * Estrae il codice lingua a 2 caratteri dal pathname Astro ("/it/funzioni" → "it").
 * Ritorna il default se non c'è prefisso valido.
 */
export function localeFromPath(pathname: string): Locale {
  const seg = pathname.split('/').filter(Boolean)[0];
  return (LOCALES as readonly string[]).includes(seg) ? (seg as Locale) : DEFAULT_LOCALE;
}

/**
 * Helper di traduzione per il locale corrente.
 * Usa chiavi dot-notation, es. `t('nav.features')`.
 * Se manca la chiave nel dizionario, fa fallback su italiano poi sulla chiave stessa.
 */
export function useT(locale: Locale) {
  const dict = dictionaries[locale];
  return function t(key: string): string {
    const v = resolve(dict, key);
    if (v !== undefined) return v;
    const fallback = resolve(dictionaries.it, key);
    return fallback ?? key;
  };
}

function resolve(dict: Dictionary, key: string): string | undefined {
  const parts = key.split('.');
  let cur: unknown = dict;
  for (const p of parts) {
    if (cur && typeof cur === 'object' && p in (cur as Record<string, unknown>)) {
      cur = (cur as Record<string, unknown>)[p];
    } else {
      return undefined;
    }
  }
  return typeof cur === 'string' ? cur : undefined;
}

/**
 * Mappa slug logico → slug localizzato per lingua.
 * Le chiavi sono indipendenti dalla lingua e usate nel codice (Header, Footer, switcher).
 */
export const ROUTES: Record<string, Record<Locale, string>> = {
  home: { it: '', en: '', es: '' },
  features: { it: 'funzioni', en: 'features', es: 'funcionalidades' },
  forWhom: { it: 'per-chi', en: 'for-whom', es: 'para-quien' },
  privacy: { it: 'privacy', en: 'privacy', es: 'privacidad' },
  faq: { it: 'faq', en: 'faq', es: 'preguntas' },
  pricing: { it: 'prezzi', en: 'pricing', es: 'precios' },
  contact: { it: 'contatti', en: 'contact', es: 'contacto' }
};

export type RouteKey = keyof typeof ROUTES;

/** Costruisce un URL relativo come `/it/funzioni`. */
export function routeUrl(key: RouteKey, locale: Locale): string {
  const slug = ROUTES[key][locale];
  return slug ? `/${locale}/${slug}` : `/${locale}`;
}

/** Trova la chiave di rotta logica da un pathname (per il LangSwitcher). */
export function routeKeyFromPath(pathname: string): RouteKey | null {
  const segs = pathname.split('/').filter(Boolean); // [locale, slug?]
  if (segs.length === 0) return null;
  const slug = segs[1] ?? '';
  for (const [key, locales] of Object.entries(ROUTES)) {
    for (const loc of LOCALES) {
      if (locales[loc] === slug) return key as RouteKey;
    }
  }
  return null;
}
