import { FormEvent, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, extractError } from '../api/client';
import { SharedUpdate, SharedUpdateAudience, SharedUpdateTemplate } from '../types';
import { useTranslation } from 'react-i18next';
import AiAssistPanel from '../components/AiAssistPanel';
import { rephrase } from '../api/ai';
import { useAiContext } from '../hooks/useAiContext';

const AUDIENCES: SharedUpdateAudience[] = ['CloseFamily','ExtendedFamily','Friends','Generic'];

const AUDIENCE_I18N_KEY: Record<SharedUpdateAudience, string> = {
  CloseFamily: 'sharedUpdates.audience.closeFamily',
  ExtendedFamily: 'sharedUpdates.audience.extendedFamily',
  Friends: 'sharedUpdates.audience.friends',
  Generic: 'sharedUpdates.audience.generic'
};

export default function SharedUpdatesPage() {
  const { id } = useParams<{ id: string }>();
  const { t, i18n } = useTranslation();
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
    if (!confirm(t('sharedUpdates.deleteConfirm'))) return;
    await api.delete(`/care-circles/${id}/shared-updates/${u.id}`);
    load();
  };

  const copy = async (text: string, key: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(key);
      setTimeout(() => setCopied(null), 2000);
    } catch {
      alert(t('sharedUpdates.copyError'));
    }
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <h1 className="text-2xl font-semibold">{t('sharedUpdates.title')}</h1>
        <Link to={`/care-circles/${id}`} className="text-sm text-accanto-500 hover:underline">{t('sharedUpdates.backToCircle')}</Link>
      </div>
      <p className="text-accanto-500 mb-1">
        {t('sharedUpdates.intro')}
      </p>
      <p className="text-accanto-500 mb-4">
        {t('sharedUpdates.introBalance')}
      </p>

      <button onClick={() => { setPrefill(''); setShowForm(s => !s); }} className="btn-primary mb-4">
        {showForm ? t('common.cancel') : t('sharedUpdates.newUpdate')}
      </button>

      {showForm && <NewForm careCircleId={id!} prefill={prefill} onCreated={() => { setShowForm(false); setPrefill(''); load(); }} />}

      <SharedUpdatesAiSection circleId={id!} />

      {templates.length > 0 && (
        <details className="card mb-4">
          <summary className="cursor-pointer font-medium">{t('sharedUpdates.templatesPanel')}</summary>
          <div className="mt-3 space-y-3">
            {templates.map(tpl => (
              <div key={tpl.title}>
                <p className="text-sm font-medium">{tpl.title}</p>
                <p className="text-sm whitespace-pre-wrap mt-1 text-accanto-700">{tpl.content}</p>
                <button
                  type="button"
                  className="btn-ghost mt-2"
                  onClick={() => { setPrefill(tpl.content); setShowForm(true); }}
                >{t('sharedUpdates.useAsBase')}</button>
              </div>
            ))}
          </div>
        </details>
      )}

      {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2 mb-3">{error}</div>}

      {items === null ? <p className="text-accanto-500">{t('common.loading')}</p> :
        items.length === 0 ? <p className="text-accanto-500">{t('sharedUpdates.empty')}</p> :
        <div className="space-y-3">
          {items.map(u => (
            <div key={u.id} className="card">
              <p className="text-xs text-accanto-500">{t(AUDIENCE_I18N_KEY[u.audience])} • {new Date(u.createdAt).toLocaleString(i18n.language)}</p>
              <p className="mt-2 whitespace-pre-wrap">{u.content}</p>
              <div className="mt-3 flex gap-2">
                <button onClick={() => copy(u.content, u.id)} className="btn-ghost">
                  {copied === u.id ? t('sharedUpdates.copied') : t('sharedUpdates.copyText')}
                </button>
                <button onClick={() => del(u)} className="text-sm text-accanto-500 hover:text-red-700">{t('common.delete')}</button>
              </div>
            </div>
          ))}
        </div>
      }
    </div>
  );
}

function NewForm({ careCircleId, prefill, onCreated }: { careCircleId: string; prefill: string; onCreated: () => void }) {
  const { t } = useTranslation();
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
        <label className="label">{t('sharedUpdates.audienceLabel')}</label>
        <select className="input" value={audience} onChange={(e) => setAudience(e.target.value as SharedUpdateAudience)}>
          {AUDIENCES.map(a => <option key={a} value={a}>{t(AUDIENCE_I18N_KEY[a])}</option>)}
        </select>
      </div>
      <div>
        <label className="label">{t('sharedUpdates.messageLabel')}</label>
        <textarea className="input min-h-[140px]" required value={content} onChange={(e) => setContent(e.target.value)} />
      </div>
      {error && <div className="text-sm text-red-700">{error}</div>}
      <button className="btn-primary" disabled={busy}>{busy ? t('common.saving') : t('sharedUpdates.save')}</button>
    </form>
  );
}

function SharedUpdatesAiSection({ circleId }: { circleId: string }) {
  const { t } = useTranslation();
  const [text, setText] = useState('');
  const [tone, setTone] = useState<'neutral' | 'warm' | 'concise' | 'hopeful' | 'encouraging'>('warm');
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
          <option value="hopeful">{t('ai.rephrase.toneOptions.hopeful')}</option>
          <option value="encouraging">{t('ai.rephrase.toneOptions.encouraging')}</option>
        </select>
      </label>
    </AiAssistPanel>
  );
}
