import { FormEvent, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';

export default function ResetPasswordPage() {
  const { t } = useTranslation();
  const nav = useNavigate();
  const [params] = useSearchParams();
  const token = useMemo(() => params.get('token') ?? '', [params]);

  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const tokenMissing = !token;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!token) return;
    if (password !== confirm) {
      setError(t('auth.passwordsDoNotMatch'));
      return;
    }
    setError(null);
    setBusy(true);
    try {
      await api.post('/auth/reset-password', { token, newPassword: password });
      setDone(true);
      setTimeout(() => nav('/login', { replace: true }), 2500);
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  if (tokenMissing) {
    return (
      <div className="max-w-md mx-auto pt-6">
        <h1 className="text-2xl font-semibold mb-2">{t('auth.resetPasswordTitle')}</h1>
        <p className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">
          {t('auth.resetPasswordTokenInvalid')}
        </p>
        <p className="mt-6 text-sm text-accanto-500">
          <Link to="/forgot-password" className="text-accanto-700 underline">
            {t('auth.forgotPasswordCta')}
          </Link>
        </p>
      </div>
    );
  }

  if (done) {
    return (
      <div className="max-w-md mx-auto pt-6">
        <h1 className="text-2xl font-semibold mb-2">{t('auth.resetPasswordTitle')}</h1>
        <p className="text-accanto-700 bg-accanto-50 border border-accanto-200 rounded-md px-3 py-3">
          {t('auth.resetPasswordSuccess')}
        </p>
      </div>
    );
  }

  return (
    <div className="max-w-md mx-auto pt-6">
      <h1 className="text-2xl font-semibold mb-2">{t('auth.resetPasswordTitle')}</h1>
      <p className="text-accanto-500 mb-6">{t('auth.resetPasswordSubtitle')}</p>
      <form onSubmit={submit} className="space-y-4">
        <div>
          <label className="label" htmlFor="reset-password">{t('auth.newPassword')}</label>
          <input
            id="reset-password"
            className="input"
            type="password"
            autoComplete="new-password"
            required
            minLength={8}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          <p className="text-xs text-accanto-500 mt-1">{t('auth.passwordHint')}</p>
        </div>
        <div>
          <label className="label" htmlFor="reset-confirm">{t('auth.confirmNewPassword')}</label>
          <input
            id="reset-confirm"
            className="input"
            type="password"
            autoComplete="new-password"
            required
            minLength={8}
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
          />
        </div>
        {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">{error}</div>}
        <button className="btn-primary w-full" disabled={busy}>
          {busy ? t('common.saving') : t('auth.resetPasswordCta')}
        </button>
      </form>
      <p className="mt-6 text-sm text-accanto-500">
        <Link to="/login" className="text-accanto-700 underline">{t('auth.backToLogin')}</Link>
      </p>
    </div>
  );
}
