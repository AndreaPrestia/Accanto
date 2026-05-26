import { FormEvent, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, extractError } from '../api/client';
import { AudienceLabel, SharedUpdate, SharedUpdateAudience, SharedUpdateTemplate } from '../types';
import { useTranslation } from 'react-i18next';
import AiAssistPanel from '../components/AiAssistPanel';
import { rephrase } from '../api/ai';
import { useAiContext } from '../hooks/useAiContext';

const AUDIENCES: SharedUpdateAudience[] = ['CloseFamily','ExtendedFamily','Friends','Generic'];

export default function SharedUpdatesPage() {
  const { id } = useParams<{ id: string }>();
  const [items, setItems] = useState<SharedUpdate[] | null>(null);
  const [templates, setTemplates] = useState<SharedUpdateTemplate[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [prefill, setPrefill] = useState<string>('');
  const [copied, setCopied] = useState<string | null>(null);

  const load = async () => {
    if (!id) return;
    try {
      const { data } = await api.get<SharedUpdate[]>(`/care-circles/${id}/shared-updates`);
      setItems(data);
    } catch (e) { setError(extractError(e)); }
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [id]);
  useEffect(() => {
    api.get<SharedUpdateTemplate[]>('/shared-update-templates').then(r => setTemplates(r.data)).catch(() => {});
  }, []);

  const del = async (u: SharedUpdate) => {
    if (!confirm('Eliminare questo aggiornamento?')) return;
    await api.delete(`/care-circles/${id}/shared-updates/${u.id}`);
    load();
  };

  const copy = async (text: string, key: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(key);
      setTimeout(() => setCopied(null), 2000);
    } catch {
      alert('Impossibile copiare automaticamente. Selezionalo e copialo a mano.');
    }
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <h1 className="text-2xl font-semibold">Aggiornamenti per gli altri</h1>
        <Link to={`/care-circles/${id}`} className="text-sm text-accanto-500 hover:underline">← Cerchio</Link>
      </div>
      <p className="text-accanto-500 mb-4">
        Componi un messaggio una volta sola, poi copialo e invialo dove preferisci.
      </p>

      <button onClick={() => { setPrefill(''); setShowForm(s => !s); }} className="btn-primary mb-4">
        {showForm ? 'Annulla' : '+ Nuovo aggiornamento'}
      </button>

      {showForm && <NewForm careCircleId={id!} prefill={prefill} onCreated={() => { setShowForm(false); setPrefill(''); load(); }} />}

      <SharedUpdatesAiSection circleId={id!} />

      {templates.length > 0 && (
        <details className="card mb-4">
          <summary className="cursor-pointer font-medium">Modelli pronti</summary>
          <div className="mt-3 space-y-3">
            {templates.map(t => (
              <div key={t.title}>
                <p className="text-sm font-medium">{t.title}</p>
                <p className="text-sm whitespace-pre-wrap mt-1 text-accanto-700">{t.content}</p>
                <button
                  type="button"
                  className="btn-ghost mt-2"
                  onClick={() => { setPrefill(t.content); setShowForm(true); }}
                >Usa come base</button>
              </div>
            ))}
          </div>
        </details>
      )}

      {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2 mb-3">{error}</div>}

      {items === null ? <p className="text-accanto-500">Caricamento…</p> :
        items.length === 0 ? <p className="text-accanto-500">Ancora nessun aggiornamento.</p> :
        <div className="space-y-3">
          {items.map(u => (
            <div key={u.id} className="card">
              <p className="text-xs text-accanto-500">{AudienceLabel[u.audience]} • {new Date(u.createdAt).toLocaleString('it-IT')}</p>
              <p className="mt-2 whitespace-pre-wrap">{u.content}</p>
              <div className="mt-3 flex gap-2">
                <button onClick={() => copy(u.content, u.id)} className="btn-ghost">
                  {copied === u.id ? 'Copiato!' : 'Copia testo'}
                </button>
                <button onClick={() => del(u)} className="text-sm text-accanto-500 hover:text-red-700">Elimina</button>
              </div>
            </div>
          ))}
        </div>
      }
    </div>
  );
}

function NewForm({ careCircleId, prefill, onCreated }: { careCircleId: string; prefill: string; onCreated: () => void }) {
  const [audience, setAudience] = useState<SharedUpdateAudience>('CloseFamily');
  const [content, setContent] = useState(prefill);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true); setError(null);
    try {
      await api.post(`/care-circles/${careCircleId}/shared-updates`, { audience, content });
      onCreated();
    } catch (err) { setError(extractError(err)); }
    finally { setBusy(false); }
  };

  return (
    <form onSubmit={submit} className="card mb-4 space-y-3">
      <div>
        <label className="label">A chi è rivolto</label>
        <select className="input" value={audience} onChange={(e) => setAudience(e.target.value as SharedUpdateAudience)}>
          {AUDIENCES.map(a => <option key={a} value={a}>{AudienceLabel[a]}</option>)}
        </select>
      </div>
      <div>
        <label className="label">Messaggio</label>
        <textarea className="input min-h-[140px]" required value={content} onChange={(e) => setContent(e.target.value)} />
      </div>
      {error && <div className="text-sm text-red-700">{error}</div>}
      <button className="btn-primary" disabled={busy}>{busy ? 'Salvataggio…' : 'Salva aggiornamento'}</button>
    </form>
  );
}

function SharedUpdatesAiSection({ circleId }: { circleId: string }) {
  const { t } = useTranslation();
  const [text, setText] = useState('');
  const [tone, setTone] = useState<'neutral' | 'warm' | 'concise'>('warm');
  const { systemAvailable, enabledForCircle, loading } = useAiContext(circleId);

  if (loading) return null;
  const disabled = !systemAvailable || !enabledForCircle;
  const disabledReason = !systemAvailable ? t('ai.disabledSystem') as string : t('ai.disabledCircle') as string;

  return (
    <AiAssistPanel
      title={t('ai.rephrase.title')}
      description={t('ai.rephrase.description') as string}
      ctaLabel={t('ai.rephrase.cta')}
      disabled={disabled}
      disabledReason={disabledReason}
      onGenerate={() => rephrase(circleId, text.trim(), tone)}
    >
      <label className="text-sm block">
        <span className="block text-accanto-700 mb-1">{t('ai.rephrase.textLabel')}</span>
        <textarea
          className="input w-full"
          rows={3}
          value={text}
          onChange={(e) => setText(e.target.value)}
        />
      </label>
      <label className="text-sm block">
        <span className="block text-accanto-700 mb-1">{t('ai.rephrase.toneLabel')}</span>
        <select className="input" value={tone} onChange={(e) => setTone(e.target.value as any)}>
          <option value="neutral">{t('ai.rephrase.toneOptions.neutral')}</option>
          <option value="warm">{t('ai.rephrase.toneOptions.warm')}</option>
          <option value="concise">{t('ai.rephrase.toneOptions.concise')}</option>
        </select>
      </label>
    </AiAssistPanel>
  );
}
