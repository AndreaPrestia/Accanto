import { FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, extractError } from '../api/client';
import { CareCircle } from '../types';

export default function NewCareCirclePage() {
  const nav = useNavigate();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const { data } = await api.post<CareCircle>('/care-circles', {
        name,
        description: description || null
      });
      nav(`/care-circles/${data.id}`, { replace: true });
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="max-w-md mx-auto pt-2">
      <h1 className="text-2xl font-semibold mb-2">Nuovo cerchio</h1>
      <p className="text-accanto-500 mb-6">
        Dai un nome al cerchio (per esempio il nome della persona che stai assistendo).
        Potrai modificarlo in qualsiasi momento.
      </p>
      <form onSubmit={submit} className="space-y-4">
        <div>
          <label className="label">Nome del cerchio</label>
          <input className="input" required minLength={2} value={name} onChange={(e) => setName(e.target.value)} placeholder="Es. Mamma" />
        </div>
        <div>
          <label className="label">Descrizione (facoltativa)</label>
          <textarea className="input min-h-[80px]" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Una breve nota per orientarti" />
        </div>
        {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">{error}</div>}
        <button className="btn-primary w-full" disabled={busy}>{busy ? 'Creazione…' : 'Crea cerchio'}</button>
      </form>
    </div>
  );
}
