import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import { CareCircle, RoleLabel, TimelineEntry } from '../types';
import InvitesPanel from '../components/InvitesPanel';
import { setCircleAiEnabled } from '../api/ai';
import { useAiContext } from '../hooks/useAiContext';

export default function CareCirclePage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const [circle, setCircle] = useState<CareCircle | null>(null);
  const [error, setError] = useState<string | null>(null);
  // `null` = ancora in caricamento; `true` = cerchio senza voci di diario
  // (proxy per "cerchio nuovo"), mostriamo l'empty state checklist invece
  // delle 5 card generiche.
  const [isEmpty, setIsEmpty] = useState<boolean | null>(null);

  useEffect(() => {
    if (!id) return;
    api.get<CareCircle>(`/care-circles/${id}`)
      .then((r) => setCircle(r.data))
      .catch((e) => setError(extractError(e)));

    // Fetch minima della timeline per capire se il cerchio è vuoto. Errori
    // silenziosi: se fallisce cadiamo comunque sulle 5 card standard.
    api.get<TimelineEntry[]>(`/care-circles/${id}/timeline`)
      .then((r) => setIsEmpty(r.data.length === 0))
      .catch(() => setIsEmpty(false));
  }, [id]);

  if (error) return <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">{error}</div>;
  if (!circle) return <p className="text-accanto-500">Caricamento…</p>;

  const isOwner = circle.myRole === 'Owner';
  const isActive = circle.status === 'Active';
  const showEmptyState = isEmpty === true && isActive;

  return (
    <div>
      <div className="flex items-baseline flex-wrap gap-x-3 gap-y-2">
        <h1 className="text-2xl font-semibold">{circle.name}</h1>
        {isActive && (
          <Link
            to={`/care-circles/${circle.id}/audit`}
            className="text-xs rounded-full border border-accanto-200 bg-accanto-50 text-accanto-700 px-2.5 py-0.5 hover:bg-accanto-100"
          >
            {t('circle.chips.audit')}
          </Link>
        )}
      </div>
      {circle.description && <p className="text-accanto-500 mt-1">{circle.description}</p>}
      <p className="text-xs text-accanto-500 mt-2">Il tuo ruolo: {RoleLabel[circle.myRole]}{circle.status === 'Archived' ? ' • archiviato' : ''}</p>

      {showEmptyState ? (
        <EmptyStateChecklist
          circleId={circle.id}
          isOwner={isOwner}
          onInvite={() => {
            const el = document.getElementById('invites-panel');
            el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
          }}
        />
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mt-6">
          <Section to={`/care-circles/${circle.id}/timeline`} title="Diario" desc="Annota appuntamenti, sintomi, decisioni." />
          <Section to={`/care-circles/${circle.id}/documents`} title="Documenti" desc="Conserva referti, esami, prescrizioni." />
          <Section to={`/care-circles/${circle.id}/questions`} title="Domande per il medico" desc="Prepara cosa chiedere alla prossima visita." />
          <Section to={`/care-circles/${circle.id}/shared-updates`} title="Aggiornamenti per gli altri" desc="Componi messaggi da copiare e inviare." />
          <Section to={`/care-circles/${circle.id}/difficult-day`} title="Giornata difficile" desc="Un piccolo respiro quando serve." emphasis />
        </div>
      )}

      {isOwner && isActive && <div id="invites-panel"><InvitesPanel circleId={circle.id} /></div>}

      {isOwner && isActive && (
        <AiCircleSettingsCard circle={circle} onChanged={(aiEnabled) => setCircle({ ...circle, aiEnabled })} />
      )}

      <ExportPdfButton circleId={circle.id} />

      {isOwner && isActive && (
        <div className="mt-8">
          <ArchiveButton id={circle.id} onArchived={() => setCircle({ ...circle, status: 'Archived' })} />
        </div>
      )}
    </div>
  );
}

/**
 * Checklist mostrata quando il cerchio è appena stato creato (0 voci
 * timeline). Rimpiazza le 5 card generiche fino a quando l'utente non
 * scrive la prima voce. Ogni item porta subito all'azione.
 */
function EmptyStateChecklist({
  circleId,
  isOwner,
  onInvite
}: {
  circleId: string;
  isOwner: boolean;
  onInvite: () => void;
}) {
  const { t } = useTranslation();
  return (
    <section className="card mt-6 space-y-4">
      <div>
        <h2 className="text-lg font-medium">{t('circle.emptyState.title')}</h2>
        <p className="text-sm text-accanto-500 mt-1">{t('circle.emptyState.subtitle')}</p>
      </div>
      <ol className="space-y-3">
        <ChecklistItem
          index={1}
          title={t('circle.emptyState.item1.title')}
          body={t('circle.emptyState.item1.body')}
          cta={t('circle.emptyState.item1.cta')}
          to={`/care-circles/${circleId}/timeline?new=1`}
        />
        <ChecklistItem
          index={2}
          title={t('circle.emptyState.item2.title')}
          body={t('circle.emptyState.item2.body')}
          cta={t('circle.emptyState.item2.cta')}
          to={`/care-circles/${circleId}/documents`}
        />
        <ChecklistItem
          index={3}
          title={t('circle.emptyState.item3.title')}
          body={t('circle.emptyState.item3.body')}
          cta={t('circle.emptyState.item3.cta')}
          onClick={isOwner ? onInvite : undefined}
          disabled={!isOwner}
          hint={!isOwner ? t('circle.emptyState.item3.hintOwnerOnly') : undefined}
        />
      </ol>
    </section>
  );
}

function ChecklistItem({
  index,
  title,
  body,
  cta,
  to,
  onClick,
  disabled,
  hint
}: {
  index: number;
  title: string;
  body: string;
  cta: string;
  to?: string;
  onClick?: () => void;
  disabled?: boolean;
  hint?: string;
}) {
  const label = (
    <span className="text-sm font-medium text-accanto-700 hover:underline">
      {cta} →
    </span>
  );
  return (
    <li className="flex items-start gap-3">
      <span
        aria-hidden="true"
        className="mt-0.5 w-6 h-6 rounded-full border border-accanto-200 text-xs flex items-center justify-center text-accanto-700 shrink-0"
      >
        {index}
      </span>
      <div className="min-w-0">
        <p className="font-medium text-accanto-900">{title}</p>
        <p className="text-sm text-accanto-500">{body}</p>
        {hint && <p className="text-xs text-accanto-500 mt-1 italic">{hint}</p>}
        <div className="mt-1">
          {disabled ? (
            <span className="text-sm text-accanto-300">{cta}</span>
          ) : to ? (
            <Link to={to}>{label}</Link>
          ) : (
            <button type="button" onClick={onClick} className="text-left">
              {label}
            </button>
          )}
        </div>
      </div>
    </li>
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

function ExportPdfButton({ circleId }: { circleId: string }) {
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const download = async () => {
    setBusy(true);
    setError(null);
    try {
      const params: Record<string, string> = {};
      if (from) params.from = new Date(from + 'T00:00:00').toISOString();
      if (to) params.to = new Date(to + 'T23:59:59.999').toISOString();
      const res = await api.get(`/care-circles/${circleId}/export/pdf`, {
        params,
        responseType: 'blob'
      });
      const disp = res.headers['content-disposition'] as string | undefined;
      const match = disp?.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i);
      const filename = match ? decodeURIComponent(match[1]) : `accanto-cerchio.pdf`;
      const url = URL.createObjectURL(res.data as Blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="mt-8 card">
      <h3 className="font-medium">Esporta in PDF</h3>
      <p className="text-sm text-accanto-500 mt-1">Un riassunto del cerchio (diario e domande aperte) da portare al medico.</p>
      <div className="grid grid-cols-2 gap-2 mt-3">
        <label className="text-sm">
          <span className="block text-accanto-700 mb-1">Dal</span>
          <input type="date" value={from} max={to || undefined} onChange={(e) => setFrom(e.target.value)} className="w-full border border-accanto-200 rounded-lg px-2 py-1" />
        </label>
        <label className="text-sm">
          <span className="block text-accanto-700 mb-1">Al</span>
          <input type="date" value={to} min={from || undefined} onChange={(e) => setTo(e.target.value)} className="w-full border border-accanto-200 rounded-lg px-2 py-1" />
        </label>
      </div>
      {error && <p className="text-sm text-red-700 mt-2">{error}</p>}
      <div className="mt-3 flex items-center gap-2">
        <button onClick={download} disabled={busy} className="px-4 py-2 rounded-lg bg-accanto-700 text-white disabled:opacity-60">
          {busy ? 'Generazione…' : 'Scarica PDF'}
        </button>
        {(from || to) && (
          <button onClick={() => { setFrom(''); setTo(''); }} className="text-sm text-accanto-700 hover:underline">Pulisci filtri</button>
        )}
      </div>
    </section>
  );
}

function AiCircleSettingsCard({ circle, onChanged }: { circle: CareCircle; onChanged: (enabled: boolean) => void }) {
  const { t } = useTranslation();
  const { systemAvailable, loading } = useAiContext();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const toggle = async () => {
    setBusy(true);
    setError(null);
    try {
      const next = !circle.aiEnabled;
      await setCircleAiEnabled(circle.id, next);
      onChanged(next);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="mt-8 card">
      <h3 className="font-medium">{t('ai.title')}</h3>
      <p className="text-sm text-accanto-500 mt-1">{t('ai.subtitle')}</p>
      {loading ? (
        <p className="text-sm text-accanto-500 mt-3">{t('common.loading')}</p>
      ) : !systemAvailable ? (
        <p className="text-sm text-accanto-500 mt-3">{t('ai.disabledSystem')}</p>
      ) : (
        <>
          <label className="flex items-center gap-2 mt-3 text-sm">
            <input type="checkbox" checked={circle.aiEnabled} onChange={toggle} disabled={busy} />
            <span>{t('ai.enableToggle')}</span>
          </label>
          <p className="text-xs text-accanto-500 mt-1">{t('ai.enableHint')}</p>
          {error && <p className="text-sm text-red-700 mt-2">{error}</p>}
        </>
      )}
    </section>
  );
}


