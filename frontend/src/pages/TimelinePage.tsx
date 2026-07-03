import { FormEvent, useEffect, useMemo, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { api, extractError } from '../api/client';
import { TimelineEntry, TimelineEntryType, TimelineTypeLabel, TimelineVisibility, VisibilityLabel } from '../types';
import { useTranslation } from 'react-i18next';
import AiAssistPanel from '../components/AiAssistPanel';
import { timelineSummary } from '../api/ai';
import { useAiContext } from '../hooks/useAiContext';

const TYPES: TimelineEntryType[] = ['MedicalUpdate','Symptom','Medication','Appointment','Decision','PersonalNote','Practical','Other'];
const VIS: TimelineVisibility[] = ['Circle','Private'];

export default function TimelinePage() {
  const { id } = useParams<{ id: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const [entries, setEntries] = useState<TimelineEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [filterType, setFilterType] = useState<TimelineEntryType | ''>('');
  const [filterTag, setFilterTag] = useState('');
  const [filterFrom, setFilterFrom] = useState('');
  const [filterTo, setFilterTo] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [selectMode, setSelectMode] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [bulkBusy, setBulkBusy] = useState(false);
  const [bulkMsg, setBulkMsg] = useState<string | null>(null);
  const [bulkAddTags, setBulkAddTags] = useState('');
  const [bulkRemoveTags, setBulkRemoveTags] = useState('');
  const [bulkVisibility, setBulkVisibility] = useState<'' | TimelineVisibility>('');

  // Deep link "quick action" dalla dashboard: `?new=1` apre subito il form
  // "Nuova voce" senza costringere l'utente a un ulteriore click. Puliamo
  // il param dopo la prima esecuzione per evitare di riaprire il form ad
  // ogni back/refresh.
  useEffect(() => {
    if (searchParams.get('new') === '1') {
      setShowForm(true);
      const next = new URLSearchParams(searchParams);
      next.delete('new');
      setSearchParams(next, { replace: true });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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

      <TimelineAiSection circleId={id!} />

      <div className="flex items-center gap-3 mb-3">
        <button
          type="button"
          onClick={() => {
            setSelectMode(m => !m);
            setSelected(new Set());
            setBulkMsg(null);
          }}
          className="text-sm text-accanto-700 hover:underline"
        >
          {selectMode ? 'Esci da selezione multipla' : 'Selezione multipla'}
        </button>
        {selectMode && entries && entries.length > 0 && (
          <button
            type="button"
            onClick={() => setSelected(new Set(entries.map(e => e.id)))}
            className="text-sm text-accanto-500 hover:underline"
          >
            Seleziona tutte ({entries.length})
          </button>
        )}
      </div>

      {selectMode && (
        <div className="card mb-4 space-y-3">
          <h2 className="font-medium">Modifica selezionate ({selected.size})</h2>
          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="label">Aggiungi tag (separati da virgola)</label>
              <input className="input" value={bulkAddTags} onChange={(e) => setBulkAddTags(e.target.value)} placeholder="urgente, controllo" />
            </div>
            <div>
              <label className="label">Rimuovi tag (separati da virgola)</label>
              <input className="input" value={bulkRemoveTags} onChange={(e) => setBulkRemoveTags(e.target.value)} placeholder="vecchio" />
            </div>
          </div>
          <div>
            <label className="label">Visibilità</label>
            <select className="input" value={bulkVisibility} onChange={(e) => setBulkVisibility(e.target.value as any)}>
              <option value="">Non modificare</option>
              {VIS.map(v => <option key={v} value={v}>{VisibilityLabel[v]}</option>)}
            </select>
          </div>
          {bulkMsg && <p className="text-sm text-accanto-700">{bulkMsg}</p>}
          <button
            type="button"
            disabled={bulkBusy || selected.size === 0}
            className="btn-primary"
            onClick={async () => {
              const addTags = bulkAddTags.split(',').map(s => s.trim()).filter(Boolean);
              const removeTags = bulkRemoveTags.split(',').map(s => s.trim()).filter(Boolean);
              if (addTags.length === 0 && removeTags.length === 0 && !bulkVisibility) {
                setBulkMsg('Specifica almeno una modifica.');
                return;
              }
              if (!confirm(`Applicare le modifiche a ${selected.size} voci?`)) return;
              setBulkBusy(true);
              setBulkMsg(null);
              try {
                const { data } = await api.patch<{ updated: number; skipped: number }>(`/care-circles/${id}/timeline/bulk`, {
                  entryIds: Array.from(selected),
                  tagsToAdd: addTags.length ? addTags : null,
                  tagsToRemove: removeTags.length ? removeTags : null,
                  newVisibility: bulkVisibility || null
                });
                setBulkMsg(`${data.updated} aggiornate, ${data.skipped} saltate.`);
                setSelected(new Set());
                setBulkAddTags('');
                setBulkRemoveTags('');
                setBulkVisibility('');
                await load();
              } catch (e) {
                setBulkMsg(extractError(e));
              } finally {
                setBulkBusy(false);
              }
            }}
          >
            {bulkBusy ? 'Applicazione…' : 'Applica modifiche'}
          </button>
        </div>
      )}

      {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2 mb-3">{error}</div>}

      {entries === null ? (
        <p className="text-accanto-500">Caricamento…</p>
      ) : entries.length === 0 ? (
        <p className="text-accanto-500">Ancora nessuna voce.</p>
      ) : (
        <div className="space-y-3">
          {entries.map(e => (
            <EntryCard
              key={e.id}
              entry={e}
              careCircleId={id!}
              onDeleted={load}
              selectMode={selectMode}
              selected={selected.has(e.id)}
              onToggleSelect={() => {
                setSelected(prev => {
                  const next = new Set(prev);
                  if (next.has(e.id)) next.delete(e.id); else next.add(e.id);
                  return next;
                });
              }}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function EntryCard({ entry, careCircleId, onDeleted, selectMode, selected, onToggleSelect }: { entry: TimelineEntry; careCircleId: string; onDeleted: () => void; selectMode: boolean; selected: boolean; onToggleSelect: () => void }) {
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
        <div className="flex items-baseline gap-2">
          {selectMode && (
            <input type="checkbox" checked={selected} onChange={onToggleSelect} className="mt-1" aria-label="Seleziona voce" />
          )}
          <div>
            <h3 className="font-medium">{entry.title}</h3>
            <p className="text-xs text-accanto-500">{when} • {TimelineTypeLabel[entry.type]} • {VisibilityLabel[entry.visibility]}</p>
          </div>
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

function TimelineAiSection({ circleId }: { circleId: string }) {
  const { t } = useTranslation();
  const [days, setDays] = useState(7);
  const { systemAvailable, enabledForCircle, loading } = useAiContext(circleId);

  if (loading) return null;
  const disabled = !systemAvailable || !enabledForCircle;
  const disabledReason = !systemAvailable ? t('ai.disabledSystem') as string : t('ai.disabledCircle') as string;

  return (
    <AiAssistPanel
      title={t('ai.timelineSummary.title')}
      description={t('ai.timelineSummary.description') as string}
      ctaLabel={t('ai.timelineSummary.cta')}
      disabled={disabled}
      disabledReason={disabledReason}
      onGenerate={() => timelineSummary(circleId, days)}
    >
      <label className="text-sm">
        <span className="block text-accanto-700 mb-1">{t('ai.timelineSummary.daysLabel')}</span>
        <input
          type="number"
          min={1}
          max={60}
          value={days}
          onChange={(e) => setDays(Math.max(1, Math.min(60, Number(e.target.value) || 7)))}
          className="input w-24"
        />
      </label>
    </AiAssistPanel>
  );
}
