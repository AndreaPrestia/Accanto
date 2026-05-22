import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, extractError } from '../api/client';
import { TimelineEntry, TimelineEntryType, TimelineTypeLabel, TimelineVisibility, VisibilityLabel } from '../types';

const TYPES: TimelineEntryType[] = ['MedicalUpdate','Symptom','Medication','Appointment','Decision','PersonalNote','Practical','Other'];
const VIS: TimelineVisibility[] = ['Circle','Private'];

export default function TimelinePage() {
  const { id } = useParams<{ id: string }>();
  const [entries, setEntries] = useState<TimelineEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [filterType, setFilterType] = useState<TimelineEntryType | ''>('');
  const [filterTag, setFilterTag] = useState('');
  const [filterFrom, setFilterFrom] = useState('');
  const [filterTo, setFilterTo] = useState('');
  const [showForm, setShowForm] = useState(false);

  const load = async () => {
    if (!id) return;
    try {
      const params: any = {};
      if (filterType) params.type = filterType;
      if (filterTag.trim()) params.tag = filterTag.trim();
      // I filtri data sono "giorno intero" in fuso locale: dalle 00:00 al 23:59:59.999.
      if (filterFrom) params.from = new Date(`${filterFrom}T00:00:00`).toISOString();
      if (filterTo) params.to = new Date(`${filterTo}T23:59:59.999`).toISOString();
      const { data } = await api.get<TimelineEntry[]>(`/care-circles/${id}/timeline`, { params });
      setEntries(data);
    } catch (e) {
      setError(extractError(e));
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [id, filterType, filterTag, filterFrom, filterTo]);

  const hasFilters = !!(filterType || filterTag.trim() || filterFrom || filterTo);
  const clearFilters = () => {
    setFilterType('');
    setFilterTag('');
    setFilterFrom('');
    setFilterTo('');
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <h1 className="text-2xl font-semibold">Diario</h1>
        <Link to={`/care-circles/${id}`} className="text-sm text-accanto-500 hover:underline">← Cerchio</Link>
      </div>
      <p className="text-accanto-500 mb-4">Tieni traccia di ciò che succede, giorno per giorno.</p>

      <div className="grid grid-cols-2 gap-2 mb-2">
        <select className="input" value={filterType} onChange={(e) => setFilterType(e.target.value as any)}>
          <option value="">Tutti i tipi</option>
          {TYPES.map(t => <option key={t} value={t}>{TimelineTypeLabel[t]}</option>)}
        </select>
        <input className="input" placeholder="Filtra per tag" value={filterTag} onChange={(e) => setFilterTag(e.target.value)} />
      </div>
      <div className="grid grid-cols-2 gap-2 mb-2">
        <div>
          <label className="label">Dal</label>
          <input className="input" type="date" value={filterFrom} max={filterTo || undefined} onChange={(e) => setFilterFrom(e.target.value)} />
        </div>
        <div>
          <label className="label">Al</label>
          <input className="input" type="date" value={filterTo} min={filterFrom || undefined} onChange={(e) => setFilterTo(e.target.value)} />
        </div>
      </div>
      {hasFilters && (
        <div className="mb-4">
          <button type="button" onClick={clearFilters} className="text-sm text-accanto-700 hover:underline">
            Pulisci filtri
          </button>
        </div>
      )}

      <button onClick={() => setShowForm(s => !s)} className="btn-primary mb-4">
        {showForm ? 'Annulla' : '+ Nuova voce'}
      </button>

      {showForm && <NewEntryForm careCircleId={id!} onCreated={() => { setShowForm(false); load(); }} />}

      {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2 mb-3">{error}</div>}

      {entries === null ? (
        <p className="text-accanto-500">Caricamento…</p>
      ) : entries.length === 0 ? (
        <p className="text-accanto-500">Ancora nessuna voce.</p>
      ) : (
        <div className="space-y-3">
          {entries.map(e => <EntryCard key={e.id} entry={e} careCircleId={id!} onDeleted={load} />)}
        </div>
      )}
    </div>
  );
}

function EntryCard({ entry, careCircleId, onDeleted }: { entry: TimelineEntry; careCircleId: string; onDeleted: () => void }) {
  const [busy, setBusy] = useState(false);
  const del = async () => {
    if (!confirm('Eliminare questa voce?')) return;
    setBusy(true);
    try {
      await api.delete(`/care-circles/${careCircleId}/timeline/${entry.id}`);
      onDeleted();
    } finally { setBusy(false); }
  };
  const when = new Date(entry.occurredAt).toLocaleString('it-IT');
  return (
    <div className="card">
      <div className="flex items-baseline justify-between gap-2">
        <div>
          <h3 className="font-medium">{entry.title}</h3>
          <p className="text-xs text-accanto-500">{when} • {TimelineTypeLabel[entry.type]} • {VisibilityLabel[entry.visibility]}</p>
        </div>
        <button onClick={del} disabled={busy} className="text-sm text-accanto-500 hover:text-red-700">Elimina</button>
      </div>
      <p className="mt-2 whitespace-pre-wrap">{entry.content}</p>
      {entry.tags.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-1">
          {entry.tags.map(t => <span key={t} className="text-xs bg-accanto-100 text-accanto-700 rounded px-2 py-0.5">{t}</span>)}
        </div>
      )}
    </div>
  );
}

function NewEntryForm({ careCircleId, onCreated }: { careCircleId: string; onCreated: () => void }) {
  const nowLocal = useMemo(() => {
    const d = new Date();
    d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
    return d.toISOString().slice(0,16);
  }, []);
  const [occurredAt, setOccurredAt] = useState(nowLocal);
  const [type, setType] = useState<TimelineEntryType>('MedicalUpdate');
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [tags, setTags] = useState('');
  const [visibility, setVisibility] = useState<TimelineVisibility>('Circle');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.post(`/care-circles/${careCircleId}/timeline`, {
        occurredAt: new Date(occurredAt).toISOString(),
        type,
        title,
        content,
        tags: tags.split(',').map(s => s.trim()).filter(Boolean),
        visibility
      });
      onCreated();
    } catch (err) {
      setError(extractError(err));
    } finally { setBusy(false); }
  };

  return (
    <form onSubmit={submit} className="card mb-4 space-y-3">
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Quando</label>
          <input className="input" type="datetime-local" required value={occurredAt} onChange={(e) => setOccurredAt(e.target.value)} />
        </div>
        <div>
          <label className="label">Tipo</label>
          <select className="input" value={type} onChange={(e) => setType(e.target.value as TimelineEntryType)}>
            {TYPES.map(t => <option key={t} value={t}>{TimelineTypeLabel[t]}</option>)}
          </select>
        </div>
      </div>
      <div>
        <label className="label">Titolo</label>
        <input className="input" required value={title} onChange={(e) => setTitle(e.target.value)} />
      </div>
      <div>
        <label className="label">Dettaglio</label>
        <textarea className="input min-h-[100px]" required value={content} onChange={(e) => setContent(e.target.value)} />
      </div>
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="label">Tag (separati da virgola)</label>
          <input className="input" value={tags} onChange={(e) => setTags(e.target.value)} placeholder="Es. visita, farmaci" />
        </div>
        <div>
          <label className="label">Visibilità</label>
          <select className="input" value={visibility} onChange={(e) => setVisibility(e.target.value as TimelineVisibility)}>
            {VIS.map(v => <option key={v} value={v}>{VisibilityLabel[v]}</option>)}
          </select>
        </div>
      </div>
      {error && <div className="text-sm text-red-700">{error}</div>}
      <button className="btn-primary" disabled={busy}>{busy ? 'Salvataggio…' : 'Salva voce'}</button>
    </form>
  );
}
