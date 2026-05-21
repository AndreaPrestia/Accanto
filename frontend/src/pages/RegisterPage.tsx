import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { extractError } from '../api/client';

export default function RegisterPage() {
  const { register } = useAuth();
  const nav = useNavigate();

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
      nav('/', { replace: true });
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="max-w-md mx-auto pt-6">
      <h1 className="text-2xl font-semibold mb-2">Crea il tuo spazio</h1>
      <p className="text-accanto-500 mb-6">
        Bastano pochi dati. Tutto rimane tuo, sui tuoi server o su quelli di chi ospita Accanto.
      </p>
      <form onSubmit={submit} className="space-y-4">
        <div>
          <label className="label">Come ti chiami</label>
          <input className="input" required minLength={2} value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
        </div>
        <div>
          <label className="label">Email</label>
          <input className="input" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>
        <div>
          <label className="label">Password</label>
          <input className="input" type="password" autoComplete="new-password" required minLength={8} value={password} onChange={(e) => setPassword(e.target.value)} />
          <p className="text-xs text-accanto-500 mt-1">Almeno 8 caratteri.</p>
        </div>
        {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">{error}</div>}
        <button className="btn-primary w-full" disabled={busy}>{busy ? 'Creazione…' : 'Crea il mio spazio'}</button>
      </form>
      <p className="mt-6 text-sm text-accanto-500">
        Hai già un accesso? <Link to="/login" className="text-accanto-700 underline">Entra</Link>
      </p>
    </div>
  );
}
