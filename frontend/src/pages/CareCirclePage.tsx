import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, extractError } from '../api/client';
import { CareCircle, RoleLabel } from '../types';
import InvitesPanel from '../components/InvitesPanel';

export default function CareCirclePage() {
  const { id } = useParams<{ id: string }>();
  const [circle, setCircle] = useState<CareCircle | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    api.get<CareCircle>(`/care-circles/${id}`)
      .then((r) => setCircle(r.data))
      .catch((e) => setError(extractError(e)));
  }, [id]);

  if (error) return <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">{error}</div>;
  if (!circle) return <p className="text-accanto-500">Caricamento…</p>;

  const isOwner = circle.myRole === 'Owner';

  return (
    <div>
      <h1 className="text-2xl font-semibold">{circle.name}</h1>
      {circle.description && <p className="text-accanto-500 mt-1">{circle.description}</p>}
      <p className="text-xs text-accanto-500 mt-2">Il tuo ruolo: {RoleLabel[circle.myRole]}{circle.status === 'Archived' ? ' • archiviato' : ''}</p>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mt-6">
        <Section to={`/care-circles/${circle.id}/timeline`} title="Diario" desc="Annota appuntamenti, sintomi, decisioni." />
        <Section to={`/care-circles/${circle.id}/documents`} title="Documenti" desc="Conserva referti, esami, prescrizioni." />
        <Section to={`/care-circles/${circle.id}/questions`} title="Domande per il medico" desc="Prepara cosa chiedere alla prossima visita." />
        <Section to={`/care-circles/${circle.id}/shared-updates`} title="Aggiornamenti per gli altri" desc="Componi messaggi da copiare e inviare." />
        <Section to={`/care-circles/${circle.id}/difficult-day`} title="Giornata difficile" desc="Un piccolo respiro quando serve." emphasis />
      </div>

      {isOwner && circle.status === 'Active' && <InvitesPanel circleId={circle.id} />}

      {isOwner && circle.status === 'Active' && (
        <div className="mt-8">
          <ArchiveButton id={circle.id} onArchived={() => setCircle({ ...circle, status: 'Archived' })} />
        </div>
      )}
    </div>
  );
}

function Section({ to, title, desc, emphasis }: { to: string; title: string; desc: string; emphasis?: boolean }) {
  return (
    <Link to={to} className={`card block hover:bg-accanto-50 ${emphasis ? 'border-accanto-500' : ''}`}>
      <h3 className="font-medium">{title}</h3>
      <p className="text-sm text-accanto-500 mt-1">{desc}</p>
    </Link>
  );
}

function ArchiveButton({ id, onArchived }: { id: string; onArchived: () => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const click = async () => {
    if (!confirm('Vuoi archiviare questo cerchio? Resterà visibile in sola lettura.')) return;
    setBusy(true);
    try {
      await api.delete(`/care-circles/${id}`);
      onArchived();
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  };
  return (
    <div>
      {error && <p className="text-sm text-red-700 mb-2">{error}</p>}
      <button onClick={click} className="btn-ghost" disabled={busy}>{busy ? 'Archiviazione…' : 'Archivia cerchio'}</button>
    </div>
  );
}
