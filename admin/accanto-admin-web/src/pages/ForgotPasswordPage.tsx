import { FormEvent, useState } from 'react';
import { Link } from 'react-router-dom';
import { forgotPassword } from '../api/endpoints';
import { ErrorBox } from '../components/ui';

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await forgotPassword(email.trim());
      // Risposta sempre neutra (anti-enumerazione lato server).
      setDone(true);
    } catch {
      setError('Impossibile completare la richiesta. Riprova.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex h-full items-center justify-center bg-accanto-50 p-4">
      <div className="card w-full max-w-sm">
        <div className="mb-1 text-sm font-semibold text-accanto-900">Accanto</div>
        <div className="mb-6 text-xs uppercase tracking-wide text-accanto-500">Imposta / reimposta password</div>

        {done ? (
          <div className="space-y-4">
            <p className="text-sm text-accanto-700">
              Se l'indirizzo corrisponde a un account admin, riceverai un'email con il link per
              impostare la password. Controlla la tua casella.
            </p>
            <Link to="/login" className="btn-ghost w-full">Torna al login</Link>
          </div>
        ) : (
          <form onSubmit={submit} className="space-y-4">
            <div>
              <label htmlFor="email" className="label">Email admin</label>
              <input
                id="email"
                type="email"
                autoComplete="username"
                required
                className="input"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
            <ErrorBox message={error} />
            <button type="submit" className="btn-primary w-full" disabled={loading}>
              {loading ? 'Invio…' : 'Invia link'}
            </button>
            <Link to="/login" className="block text-center text-xs text-accanto-500 hover:underline">
              Torna al login
            </Link>
          </form>
        )}
      </div>
    </div>
  );
}
