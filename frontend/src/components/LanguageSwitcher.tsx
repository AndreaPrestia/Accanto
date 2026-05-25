import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { LANGUAGE_LABEL, SUPPORTED_LANGUAGES, SupportedLanguage } from '../i18n';

interface Props {
  compact?: boolean;
}

export default function LanguageSwitcher({ compact = false }: Props) {
  const { i18n, t } = useTranslation();
  const { user, setLanguage } = useAuth();
  const [busy, setBusy] = useState(false);
  const current = (SUPPORTED_LANGUAGES as readonly string[]).includes(i18n.resolvedLanguage ?? '')
    ? (i18n.resolvedLanguage as SupportedLanguage)
    : 'it';

  const change = async (lang: SupportedLanguage) => {
    if (lang === current) return;
    setLanguage(lang);
    if (user) {
      setBusy(true);
      try {
        await api.put('/account/language', { language: lang });
      } catch {
        // Anche se la persistenza fallisce, il cambio in app resta attivo.
      } finally {
        setBusy(false);
      }
    }
  };

  return (
    <label className={compact ? 'text-sm' : 'block'}>
      {!compact && <span className="block text-sm text-accanto-700 mb-1">{t('common.language')}</span>}
      <select
        value={current}
        disabled={busy}
        onChange={(e) => change(e.target.value as SupportedLanguage)}
        className={compact
          ? 'bg-transparent text-accanto-700 text-sm border border-accanto-200 rounded px-1 py-0.5'
          : 'w-full border border-accanto-200 rounded-lg px-3 py-2'}
        aria-label={t('common.language')}
      >
        {SUPPORTED_LANGUAGES.map(l => (
          <option key={l} value={l}>{LANGUAGE_LABEL[l]}</option>
        ))}
      </select>
    </label>
  );
}
