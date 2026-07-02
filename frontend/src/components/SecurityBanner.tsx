import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api } from '../api/client';
import type { TwoFactorStatus } from '@accanto/shared/types';

/**
 * Banner dismissibile che invita ad attivare la verifica in due passaggi.
 *
 * Renderizzato in cima alla Dashboard. Compare solo se:
 *  - l'utente NON ha 2FA attiva
 *  - non ha dismesso il banner negli ultimi 30 giorni (persistenza in localStorage)
 *
 * La CTA porta ad `/account#section-twofactor` (AccountPage fa scroll).
 */
const DISMISS_KEY = 'accanto.securityBanner.dismissedAt';
const DISMISS_WINDOW_MS = 30 * 24 * 60 * 60 * 1000; // 30 giorni

function isRecentlyDismissed(): boolean {
  try {
    const raw = localStorage.getItem(DISMISS_KEY);
    if (!raw) return false;
    const ts = Number(raw);
    if (!Number.isFinite(ts)) return false;
    return Date.now() - ts < DISMISS_WINDOW_MS;
  } catch {
    return false;
  }
}

export default function SecurityBanner() {
  const { t } = useTranslation();
  const [show, setShow] = useState(false);

  useEffect(() => {
    let cancelled = false;

    const refresh = async () => {
      if (isRecentlyDismissed()) {
        if (!cancelled) setShow(false);
        return;
      }
      try {
        const r = await api.get<TwoFactorStatus>('/account/2fa');
        if (!cancelled) setShow(!r.data.enabled);
      } catch {
        // Silenzioso: se la chiamata fallisce (offline, 401, rate limit) non
        // vogliamo mostrare rumore extra sulla Dashboard.
      }
    };

    refresh();

    // Se l'utente attiva/disattiva 2FA in un'altra tab, o torna sulla tab
    // Dashboard dopo aver cambiato lo stato altrove, riprendiamo lo stato
    // aggiornato senza aspettare un remount.
    const onFocus = () => refresh();
    window.addEventListener('focus', onFocus);
    document.addEventListener('visibilitychange', onFocus);

    return () => {
      cancelled = true;
      window.removeEventListener('focus', onFocus);
      document.removeEventListener('visibilitychange', onFocus);
    };
  }, []);

  if (!show) return null;

  const dismiss = () => {
    try {
      localStorage.setItem(DISMISS_KEY, String(Date.now()));
    } catch {
      /* localStorage bloccato: pazienza, sparirà solo per la sessione */
    }
    setShow(false);
  };

  return (
    <div className="mb-4 rounded-md border border-amber-200 bg-amber-50 p-4 flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3">
      <div>
        <p className="text-sm font-medium text-amber-900">{t('security.banner.title')}</p>
        <p className="text-sm text-amber-800 mt-1">{t('security.banner.body')}</p>
      </div>
      <div className="flex items-center gap-2 shrink-0">
        <Link
          to="/account#section-twofactor"
          className="rounded-md bg-amber-700 text-white text-sm font-medium px-3 py-2 hover:bg-amber-800"
        >
          {t('security.banner.cta')}
        </Link>
        <button
          type="button"
          onClick={dismiss}
          className="text-sm text-amber-800 underline"
        >
          {t('security.banner.dismiss')}
        </button>
      </div>
    </div>
  );
}
