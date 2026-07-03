/**
 * Preferenza "Testo più grande" (accessibility, Phase 4).
 *
 * Persistita in localStorage con la chiave `accanto.largeText`.
 * Applicata come class `accanto-large` su `<html>`; la regola CSS in
 * `index.css` porta il font-size base da 16px a 18px. La UI Tailwind
 * (rem-based) scala di conseguenza.
 *
 * Non richiede backend: è una preferenza cosmetica per-dispositivo. Se in
 * futuro serve sync tra dispositivi si aggiunge una colonna
 * `user_preferences.large_text` e si sostituisce lo storage.
 */
const KEY = 'accanto.largeText';
const CLASS = 'accanto-large';

export function isLargeText(): boolean {
  try {
    return localStorage.getItem(KEY) === '1';
  } catch {
    return false;
  }
}

export function setLargeText(enabled: boolean): void {
  try {
    if (enabled) localStorage.setItem(KEY, '1');
    else localStorage.removeItem(KEY);
  } catch {
    /* localStorage bloccato: pazienza, resta solo per la sessione */
  }
  applyLargeText(enabled);
}

/** Applica/rimuove la class sul documento senza toccare localStorage. */
export function applyLargeText(enabled: boolean): void {
  const html = document.documentElement;
  if (enabled) html.classList.add(CLASS);
  else html.classList.remove(CLASS);
}

/** Applica la preferenza salvata. Da chiamare al bootstrap dell'app. */
export function initLargeText(): void {
  applyLargeText(isLargeText());
}
