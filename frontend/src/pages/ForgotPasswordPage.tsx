import { FormEvent, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';

export default function ForgotPasswordPage() {
  const { t } = useTranslation();
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await api.post('/auth/forgot-password', { email });
      setSent(true);
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  if (sent) {
    return (
      <div className="max-w-md mx-auto pt-6">
        <h1 className="text-2xl font-semibold mb-2">{t('auth.forgotPasswordTitle')}</h1>
        <p className="text-accanto-700 bg-accanto-50 border border-accanto-200 rounded-md px-3 py-3">
          {t('auth.forgotPasswordSent')}
        </p>
        <p className="mt-6 text-sm text-accanto-500">
          <Link to="/login" className="text-accanto-700 underline">{t('auth.backToLogin')}</Link>
        </p>
      </div>
    );
  }

  return (
    <div className="max-w-md mx-auto pt-6">
      <h1 className="text-2xl font-semibold mb-2">{t('auth.forgotPasswordTitle')}</h1>
      <p className="text-accanto-500 mb-6">{t('auth.forgotPasswordSubtitle')}</p>
      <form onSubmit={submit} className="space-y-4">
        <div>
          <label className="label" htmlFor="forgot-email">{t('auth.email')}</label>
          <input
            id="forgot-email"
            className="input"
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </div>
        {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">{error}</div>}
        <button className="btn-primary w-full" disabled={busy}>
          {busy ? t('common.saving') : t('auth.forgotPasswordCta')}
        </button>
      </form>
      <p className="mt-6 text-sm text-accanto-500">
        <Link to="/login" className="text-accanto-700 underline">{t('auth.backToLogin')}</Link>
      </p>
    </div>
  );
}
