import { FormEvent, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../auth/AuthContext';
import { extractError } from '../api/client';
import { hasSeenWelcome } from './WelcomePage';

export default function RegisterPage() {
  const { register } = useAuth();
  const { t } = useTranslation();
  const nav = useNavigate();
  const [params] = useSearchParams();
  const returnTo = params.get('returnTo') ?? '/';

  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await register({ email, displayName, password });
      // Se non c'è un `returnTo` esplicito (es. da un deep link invite) e
      // l'utente non ha già visto il welcome, mandiamo prima al Welcome.
      const shouldWelcome = returnTo === '/' && !hasSeenWelcome();
      nav(shouldWelcome ? '/welcome' : returnTo, { replace: true });
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="max-w-md mx-auto pt-6">
      <h1 className="text-2xl font-semibold mb-2">{t('auth.registerTitle')}</h1>
      <p className="text-accanto-500 mb-6">
        {t('auth.registerSubtitle')}
      </p>
      <form onSubmit={submit} className="space-y-4">
        <div>
          <label className="label" htmlFor="register-name">{t('auth.yourName')}</label>
          <input id="register-name" className="input" required minLength={2} value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
        </div>
        <div>
          <label className="label" htmlFor="register-email">{t('auth.email')}</label>
          <input id="register-email" className="input" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>
        <div>
          <label className="label" htmlFor="register-password">{t('auth.password')}</label>
          <input id="register-password" className="input" type="password" autoComplete="new-password" required minLength={8} value={password} onChange={(e) => setPassword(e.target.value)} />
          <p className="text-xs text-accanto-500 mt-1">{t('auth.passwordHint')}</p>
        </div>
        {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">{error}</div>}
        <button className="btn-primary w-full" disabled={busy}>{busy ? t('auth.creating') : t('auth.registerCta')}</button>
      </form>
      <p className="mt-6 text-sm text-accanto-500">
        {t('auth.hasAccount')} <Link to="/login" className="text-accanto-700 underline">{t('auth.signIn')}</Link>
      </p>
    </div>
  );
}
