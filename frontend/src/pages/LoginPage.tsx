import { FormEvent, useState } from 'react';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { extractError } from '../api/client';

export default function LoginPage() {
  const { login } = useAuth();
  const nav = useNavigate();
  const loc = useLocation();
  const [params] = useSearchParams();
  const returnTo = params.get('returnTo');
  const from = returnTo ?? (loc.state as any)?.from?.pathname ?? '/';

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      await login({ email, password });
      nav(from, { replace: true });
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="max-w-md mx-auto pt-6">
      <h1 className="text-2xl font-semibold mb-2">Bentornato</h1>
      <p className="text-accanto-500 mb-6">Accanto è qui per aiutarti a non perdere il filo.</p>
      <form onSubmit={submit} className="space-y-4">
        <div>
          <label className="label">Email</label>
          <input className="input" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>
        <div>
          <label className="label">Password</label>
          <input className="input" type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        </div>
        {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">{error}</div>}
        <button className="btn-primary w-full" disabled={busy}>{busy ? 'Accesso in corso…' : 'Entra'}</button>
      </form>
      <p className="mt-6 text-sm text-accanto-500">
        Non hai ancora un accesso? <Link to="/register" className="text-accanto-700 underline">Crea il tuo spazio</Link>
      </p>
    </div>
  );
}
