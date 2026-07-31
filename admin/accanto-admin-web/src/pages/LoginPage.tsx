import { FormEvent, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AxiosError } from 'axios';
import { useAuth } from '../auth/AuthContext';
import { ErrorBox } from '../components/ui';

function mapError(err: unknown): string {
  const ax = err as AxiosError;
  const status = ax?.response?.status;
  if (status === 401) return 'Invalid credentials.';
  if (status === 403) return 'Admin account is disabled.';
  if (status === 429) return 'Too many attempts. Please wait and try again.';
  if (ax?.code === 'ERR_NETWORK') return 'Cannot reach the Admin API.';
  return 'Login failed. Please try again.';
}

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await login({ email: email.trim(), password });
      navigate('/dashboard', { replace: true });
    } catch (err) {
      setError(mapError(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex h-full items-center justify-center bg-accanto-50 p-4">
      <form onSubmit={submit} className="card w-full max-w-sm">
        <div className="mb-1 text-sm font-semibold text-accanto-900">Accanto</div>
        <div className="mb-6 text-xs uppercase tracking-wide text-accanto-500">Control Plane sign in</div>

        <div className="space-y-4">
          <div>
            <label htmlFor="email" className="label">Email</label>
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
          <div>
            <label htmlFor="password" className="label">Password</label>
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              required
              className="input"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>
        </div>

        <div className="mt-4">
          <ErrorBox message={error} />
        </div>

        <button type="submit" className="btn-primary mt-5 w-full" disabled={loading}>
          {loading ? 'Signing in…' : 'Sign in'}
        </button>

        <Link to="/forgot-password" className="mt-3 block text-center text-xs text-accanto-600 hover:underline">
          Password dimenticata / primo accesso?
        </Link>

        <p className="mt-4 text-center text-xs text-accanto-500">
          Technical access only. All actions are audited.
        </p>
      </form>
    </div>
  );
}
