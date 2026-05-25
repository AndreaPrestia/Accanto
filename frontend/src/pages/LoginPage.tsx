import { FormEvent, useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../auth/AuthContext';
import { extractError } from '../api/client';

export default function LoginPage() {
  const { login, completeTwoFactor } = useAuth();
  const { t } = useTranslation();
  const nav = useNavigate();
  const loc = useLocation();
  const [params] = useSearchParams();
  const returnTo = params.get('returnTo');
  const from = returnTo ?? (loc.state as any)?.from?.pathname ?? '/';

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Step 2FA
  const [twoFactorToken, setTwoFactorToken] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [useRecovery, setUseRecovery] = useState(false);
  const [recoveryCode, setRecoveryCode] = useState('');

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const result = await login({ email, password });
      if (result.requiresTwoFactor && result.twoFactorToken) {
        setTwoFactorToken(result.twoFactorToken);
      } else {
        nav(from, { replace: true });
      }
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  const submitTwoFactor = async (e: FormEvent) => {
    e.preventDefault();
    if (!twoFactorToken) return;
    setError(null);
    setBusy(true);
    try {
      await completeTwoFactor(
        twoFactorToken,
        useRecovery ? undefined : code,
        useRecovery ? recoveryCode : undefined
      );
      nav(from, { replace: true });
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  if (twoFactorToken) {
    return (
      <div className="max-w-md mx-auto pt-6">
        <h1 className="text-2xl font-semibold mb-2">{t('auth.twoFactorTitle')}</h1>
        <p className="text-accanto-500 mb-6">{t('auth.twoFactorSubtitle')}</p>
        <form onSubmit={submitTwoFactor} className="space-y-4">
          {!useRecovery ? (
            <div>
              <label className="label">{t('auth.twoFactorCode')}</label>
              <input
                className="input"
                inputMode="numeric"
                autoComplete="one-time-code"
                pattern="[0-9 ]*"
                maxLength={8}
                required
                value={code}
                onChange={(e) => setCode(e.target.value)}
                autoFocus
              />
            </div>
          ) : (
            <div>
              <label className="label">{t('auth.twoFactorRecoveryCode')}</label>
              <input
                className="input"
                autoComplete="off"
                required
                value={recoveryCode}
                onChange={(e) => setRecoveryCode(e.target.value)}
                autoFocus
              />
            </div>
          )}
          {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">{error}</div>}
          <button className="btn-primary w-full" disabled={busy}>{busy ? t('auth.loggingIn') : t('auth.twoFactorVerify')}</button>
          <button
            type="button"
            className="text-sm text-accanto-700 underline"
            onClick={() => { setUseRecovery(!useRecovery); setError(null); setCode(''); setRecoveryCode(''); }}
          >
            {useRecovery ? t('auth.twoFactorUseCode') : t('auth.twoFactorUseRecovery')}
          </button>
        </form>
      </div>
    );
  }

  return (
    <div className="max-w-md mx-auto pt-6">
      <h1 className="text-2xl font-semibold mb-2">{t('auth.loginTitle')}</h1>
      <p className="text-accanto-500 mb-6">{t('auth.loginSubtitle')}</p>
      <form onSubmit={submit} className="space-y-4">
        <div>
          <label className="label">{t('auth.email')}</label>
          <input className="input" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>
        <div>
          <label className="label">{t('auth.password')}</label>
          <input className="input" type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        </div>
        {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">{error}</div>}
        <button className="btn-primary w-full" disabled={busy}>{busy ? t('auth.loggingIn') : t('auth.loginCta')}</button>
      </form>
      <p className="mt-6 text-sm text-accanto-500">
        {t('auth.noAccount')} <Link to="/register" className="text-accanto-700 underline">{t('auth.createSpace')}</Link>
      </p>
    </div>
  );
}
