import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, extractError } from '../api/client';

type AuditEntry = {
  id: string;
  careCircleId: string;
  performedByUserId: string;
  performedByDisplayName: string | null;
  actionType: string;
  resourceType: string;
  resourceId: string | null;
  summary: string | null;
  timestamp: string;
};

type Page = {
  items: AuditEntry[];
  total: number;
  skip: number;
  take: number;
};

const PAGE_SIZE = 50;

const ACTION_LABEL: Record<string, string> = {
  CircleCreated: 'Ha creato il cerchio',
  CircleUpdated: 'Ha modificato il cerchio',
  CircleArchived: 'Ha archiviato il cerchio',
  MemberJoined: 'È entrato nel cerchio',
  InviteCreated: 'Ha creato un invito',
  InviteRevoked: 'Ha revocato un invito',
  EntryCreated: 'Ha aggiunto una voce al diario',
  EntryUpdated: 'Ha modificato una voce del diario',
  EntryDeleted: 'Ha eliminato una voce del diario',
  EntriesBulkUpdated: 'Ha aggiornato più voci del diario',
  DocumentUploaded: 'Ha caricato un documento',
  DocumentDeleted: 'Ha eliminato un documento',
  QuestionCreated: 'Ha aggiunto una domanda per il medico',
  QuestionUpdated: 'Ha modificato una domanda per il medico',
  QuestionDeleted: 'Ha eliminato una domanda per il medico',
  UpdateCreated: 'Ha pubblicato un aggiornamento',
  UpdateDeleted: 'Ha eliminato un aggiornamento',
  DataExported: 'Ha esportato i propri dati'
};

export default function AuditPage() {
  const { id } = useParams<{ id: string }>();
  const [items, setItems] = useState<AuditEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [skip, setSkip] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async (from: number, replace: boolean) => {
    if (!id) return;
    setLoading(true);
    try {
      const { data } = await api.get<Page>(`/care-circles/${id}/audit`, {
        params: { skip: from, take: PAGE_SIZE }
      });
      setTotal(data.total);
      setSkip(from + data.items.length);
      setItems(prev => replace ? data.items : [...prev, ...data.items]);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(0, true); /* eslint-disable-next-line */ }, [id]);

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <h1 className="text-2xl font-semibold">Registro azioni</h1>
        <Link to={`/care-circles/${id}`} className="text-sm text-accanto-500 hover:underline">← Cerchio</Link>
      </div>
      <p className="text-accanto-500 mb-4">
        Le azioni svolte dai membri del cerchio, dalla più recente.
      </p>

      {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2 mb-3">{error}</div>}

      {items.length === 0 && !loading ? (
        <p className="text-accanto-500">Nessuna azione registrata.</p>
      ) : (
        <ul className="space-y-2">
          {items.map(e => (
            <li key={e.id} className="card">
              <p className="text-sm">
                <span className="font-medium">{e.performedByDisplayName ?? 'Membro rimosso'}</span>
                {' — '}
                <span>{ACTION_LABEL[e.actionType] ?? e.actionType}</span>
              </p>
              {e.summary && <p className="text-sm text-accanto-700 mt-1 whitespace-pre-wrap">{e.summary}</p>}
              <p className="text-xs text-accanto-500 mt-1">{new Date(e.timestamp).toLocaleString('it-IT')}</p>
            </li>
          ))}
        </ul>
      )}

      {skip < total && (
        <div className="mt-4">
          <button onClick={() => load(skip, false)} className="btn-ghost" disabled={loading}>
            {loading ? 'Caricamento…' : 'Carica altre'}
          </button>
        </div>
      )}
    </div>
  );
}
