import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, extractError } from '../api/client';
import { useAuth } from '../auth/AuthContext';

export default function AccountPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  // Cambio password
  const [currentPwd, setCurrentPwd] = useState('');
  const [newPwd, setNewPwd] = useState('');
  const [newPwd2, setNewPwd2] = useState('');
  const [pwdMsg, setPwdMsg] = useState<string | null>(null);
  const [pwdError, setPwdError] = useState<string | null>(null);
  const [pwdSubmitting, setPwdSubmitting] = useState(false);

  // Eliminazione account
  const [deletePwd, setDeletePwd] = useState('');
  const [understood, setUnderstood] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleteSubmitting, setDeleteSubmitting] = useState(false);

  if (!user) return null;

  async function submitPassword(e: React.FormEvent) {
    e.preventDefault();
    setPwdMsg(null);
    setPwdError(null);

    if (newPwd !== newPwd2) {
      setPwdError('Le due nuove password non coincidono.');
      return;
    }

    setPwdSubmitting(true);
    try {
      await api.post('/api/account/change-password', {
        currentPassword: currentPwd,
        newPassword: newPwd
      });
      setPwdMsg('Password aggiornata.');
      setCurrentPwd('');
      setNewPwd('');
      setNewPwd2('');
    } catch (e) {
      setPwdError(extractError(e));
    } finally {
      setPwdSubmitting(false);
    }
  }

  async function submitDelete(e: React.FormEvent) {
    e.preventDefault();
    setDeleteError(null);

    if (!understood) {
      setDeleteError('Conferma di aver compreso che l\u2019operazione \u00e8 irreversibile.');
      return;
    }

    setDeleteSubmitting(true);
    try {
      await api.delete('/api/account', { data: { currentPassword: deletePwd } });
      logout();
      navigate('/login', { replace: true });
    } catch (e) {
      setDeleteError(extractError(e));
    } finally {
      setDeleteSubmitting(false);
    }
  }

  return (
    <div className="space-y-8">
      <header>
        <h1 className="text-xl font-semibold text-accanto-900">Il tuo account</h1>
        <p className="text-sm text-accanto-500 mt-1">{user.email}</p>
      </header>

      <section className="space-y-3">
        <h2 className="text-base font-semibold text-accanto-900">Cambia password</h2>
        <form onSubmit={submitPassword} className="space-y-3">
          <div>
            <label className="block text-sm text-accanto-700 mb-1">Password attuale</label>
            <input
              type="password"
              value={currentPwd}
              onChange={(e) => setCurrentPwd(e.target.value)}
              required
              autoComplete="current-password"
              className="w-full border border-accanto-200 rounded-lg px-3 py-2"
            />
          </div>
          <div>
            <label className="block text-sm text-accanto-700 mb-1">Nuova password</label>
            <input
              type="password"
              value={newPwd}
              onChange={(e) => setNewPwd(e.target.value)}
              required
              minLength={8}
              autoComplete="new-password"
              className="w-full border border-accanto-200 rounded-lg px-3 py-2"
            />
            <p className="text-xs text-accanto-500 mt-1">Almeno 8 caratteri.</p>
          </div>
          <div>
            <label className="block text-sm text-accanto-700 mb-1">Conferma nuova password</label>
            <input
              type="password"
              value={newPwd2}
              onChange={(e) => setNewPwd2(e.target.value)}
              required
              minLength={8}
              autoComplete="new-password"
              className="w-full border border-accanto-200 rounded-lg px-3 py-2"
            />
          </div>
          {pwdError && <p className="text-sm text-red-700">{pwdError}</p>}
          {pwdMsg && <p className="text-sm text-green-700">{pwdMsg}</p>}
          <button
            type="submit"
            disabled={pwdSubmitting}
            className="w-full sm:w-auto px-4 py-2 rounded-lg bg-accanto-700 text-white disabled:opacity-60"
          >
            {pwdSubmitting ? 'Salvataggio\u2026' : 'Aggiorna password'}
          </button>
        </form>
      </section>

      <section className="space-y-3 border-t border-accanto-100 pt-6">
        <h2 className="text-base font-semibold text-red-800">Elimina account</h2>
        <p className="text-sm text-accanto-700">
          L'eliminazione rimuove definitivamente il tuo profilo e tutti i cerchi di cui sei l'unico membro,
          insieme a diario, documenti, domande e aggiornamenti collegati.
          Se condividi un cerchio con altre persone, devi prima farli uscire o uscire tu stesso.
        </p>
        <form onSubmit={submitDelete} className="space-y-3">
          <div>
            <label className="block text-sm text-accanto-700 mb-1">Conferma con la password</label>
            <input
              type="password"
              value={deletePwd}
              onChange={(e) => setDeletePwd(e.target.value)}
              required
              autoComplete="current-password"
              className="w-full border border-accanto-200 rounded-lg px-3 py-2"
            />
          </div>
          <label className="flex items-start gap-2 text-sm text-accanto-700">
            <input
              type="checkbox"
              checked={understood}
              onChange={(e) => setUnderstood(e.target.checked)}
              className="mt-1"
            />
            <span>Capisco che l'operazione &egrave; irreversibile.</span>
          </label>
          {deleteError && <p className="text-sm text-red-700">{deleteError}</p>}
          <button
            type="submit"
            disabled={deleteSubmitting || !understood}
            className="w-full sm:w-auto px-4 py-2 rounded-lg bg-red-700 text-white disabled:opacity-60"
          >
            {deleteSubmitting ? 'Eliminazione\u2026' : 'Elimina il mio account'}
          </button>
        </form>
      </section>
    </div>
  );
}
