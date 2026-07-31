import { FormEvent, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { AxiosError } from 'axios';
import { resetPassword } from '../api/endpoints';
import { ErrorBox } from '../components/ui';

const MIN_PASSWORD = 8;

function mapError(err: unknown): string {
  const ax = err as AxiosError;
  const status = ax?.response?.status;
  if (status === 403) return 'Link non valido o scaduto. Richiedine uno nuovo.';
  if (status === 422) return `La password deve avere almeno ${MIN_PASSWORD} caratteri.`;
  return 'Impossibile impostare la password. Riprova.';
}

export default function ResetPasswordPage() {
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const navigate = useNavigate();

  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const missingToken = !token;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (password.length < MIN_PASSWORD) {
      setError(`La password deve avere almeno ${MIN_PASSWORD} caratteri.`);
      return;
    }
    if (password !== confirm) {
      setError('Le password non coincidono.');
      return;
    }
    setLoading(true);
    try {
      await resetPassword(token, password);
      navigate('/login', { replace: true });
    } catch (err) {
      setError(mapError(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex h-full items-center justify-center bg-accanto-50 p-4">
      <div className="card w-full max-w-sm">
        <div className="mb-1 text-sm font-semibold text-accanto-900">Accanto</div>
        <div className="mb-6 text-xs uppercase tracking-wide text-accanto-500">Imposta password</div>

        {missingToken ? (
          <div className="space-y-4">
            <ErrorBox message="Link non valido: token mancante." />
            <Link to="/forgot-password" className="btn-ghost w-full">Richiedi un nuovo link</Link>
          </div>
        ) : (
          <form onSubmit={submit} className="space-y-4">
            <div>
              <label htmlFor="password" className="label">Nuova password</label>
              <input
                id="password"
                type="password"
                autoComplete="new-password"
                required
                className="input"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </div>
            <div>
              <label htmlFor="confirm" className="label">Conferma password</label>
              <input
                id="confirm"
                type="password"
                autoComplete="new-password"
                required
                className="input"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
              />
            </div>
            <ErrorBox message={error} />
            <button type="submit" className="btn-primary w-full" disabled={loading}>
              {loading ? 'Salvataggio…' : 'Imposta password'}
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
