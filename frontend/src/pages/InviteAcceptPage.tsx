import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { api, extractError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { CareCircleInvitePreview, RoleLabel } from '../types';

export default function InviteAcceptPage() {
  const { token } = useParams<{ token: string }>();
  const { user, loading } = useAuth();
  const nav = useNavigate();

  const [preview, setPreview] = useState<CareCircleInvitePreview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [accepting, setAccepting] = useState(false);

  useEffect(() => {
    if (!token) return;
    api
      .get<CareCircleInvitePreview>(`/invites/${token}/preview`)
      .then((r) => setPreview(r.data))
      .catch((e) => setError(extractError(e)));
  }, [token]);

  if (!token) {
    return <p className="text-sm text-red-700">Link di invito non valido.</p>;
  }

  if (error) {
    return (
      <div className="max-w-md mx-auto pt-4">
        <h1 className="text-xl font-semibold">Invito non disponibile</h1>
        <p className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2 mt-3">{error}</p>
        <Link to="/" className="btn-ghost mt-4 inline-block">Torna alla home</Link>
      </div>
    );
  }

  if (!preview || loading) {
    return <p className="text-accanto-500">Caricamento…</p>;
  }

  const expires = new Date(preview.expiresAt).toLocaleDateString('it-IT', {
    day: '2-digit',
    month: 'long',
    year: 'numeric'
  });

  const accept = async () => {
    setAccepting(true);
    setError(null);
    try {
      const { data } = await api.post<{ careCircleId: string }>(`/invites/${token}/accept`, {});
      nav(`/care-circles/${data.careCircleId}`, { replace: true });
    } catch (e) {
      setError(extractError(e));
    } finally {
      setAccepting(false);
    }
  };

  return (
    <div className="max-w-md mx-auto pt-4">
      <h1 className="text-2xl font-semibold">Sei stato invitata/o</h1>
      <p className="text-accanto-500 mt-2">
        <strong>{preview.invitedByDisplayName}</strong> vorrebbe che ti unissi al cerchio di cura
        <strong> {preview.circleName}</strong> come <strong>{RoleLabel[preview.role]}</strong>.
      </p>
      <p className="text-xs text-accanto-500 mt-1">Il link è valido fino al {expires}.</p>

      {user ? (
        <div className="mt-6 space-y-3">
          <button className="btn-primary w-full" onClick={accept} disabled={accepting}>
            {accepting ? 'Sto entrando…' : 'Entra nel cerchio'}
          </button>
          <Link to="/" className="btn-ghost w-full inline-block text-center">Non adesso</Link>
        </div>
      ) : (
        <div className="mt-6 space-y-3">
          <p className="text-sm text-accanto-500">Per accettare devi prima entrare in Accanto.</p>
          <Link
            to={`/login?returnTo=${encodeURIComponent(`/invite/${token}`)}`}
            className="btn-primary w-full inline-block text-center"
          >
            Accedi e accetta
          </Link>
          <Link
            to={`/register?returnTo=${encodeURIComponent(`/invite/${token}`)}`}
            className="btn-ghost w-full inline-block text-center"
          >
            Non ho un accesso, registrami
          </Link>
        </div>
      )}
    </div>
  );
}
