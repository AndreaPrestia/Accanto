import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, useSearchParams, Link } from 'react-router-dom';
import {
  listAiInteractions,
  getAiInteraction,
  AiInteractionSummary,
  AiInteractionDetail,
} from '../api/ai';
import { extractError } from '../api/client';

export default function AiHistoryPage() {
  const { t, i18n } = useTranslation();
  const { circleId } = useParams<{ circleId?: string }>();
  const [params, setParams] = useSearchParams();
  const page = Math.max(1, parseInt(params.get('page') ?? '1', 10) || 1);
  const pageSize = 20;

  const [items, setItems] = useState<AiInteractionSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<AiInteractionDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    listAiInteractions({ circleId, page, pageSize })
      .then((r) => {
        if (!cancelled) {
          setItems(r.items);
          setTotal(r.total);
        }
      })
      .catch((e) => !cancelled && setError(extractError(e)))
      .finally(() => !cancelled && setLoading(false));
    return () => {
      cancelled = true;
    };
  }, [circleId, page]);

  const open = async (id: string) => {
    setDetailLoading(true);
    try {
      const d = await getAiInteraction(id);
      setSelected(d);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setDetailLoading(false);
    }
  };

  const fmtDate = (s: string) => new Date(s).toLocaleString(i18n.language);
  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  return (
    <main className="container py-6">
      <h1 className="page-title">{t('ai.history.title')}</h1>
      <p className="text-sm text-accanto-500 mt-1">{t('ai.history.subtitle')}</p>

      {circleId && (
        <p className="text-xs text-accanto-500 mt-2">
          <Link to={`/care-circles/${circleId}`} className="text-accanto-700 underline">← {t('common.back')}</Link>
          {' · '}
          <span>{t('ai.history.circleSectionHint')}</span>
        </p>
      )}

      {error && (
        <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2 mt-3">{error}</div>
      )}

      {loading ? (
        <p className="mt-4 text-sm text-accanto-500">…</p>
      ) : items.length === 0 ? (
        <p className="mt-4 text-sm text-accanto-500">{t('ai.history.empty')}</p>
      ) : (
        <div className="mt-4 overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="text-left text-xs uppercase tracking-wide text-accanto-500">
              <tr>
                <th className="py-2 pr-3">{t('ai.history.when')}</th>
                <th className="py-2 pr-3">{t('ai.history.function')}</th>
                <th className="py-2 pr-3">{t('ai.history.verdict')}</th>
                <th className="py-2 pr-3">{t('ai.history.feedback')}</th>
                <th className="py-2 pr-3">{t('ai.history.model')}</th>
                <th className="py-2"></th>
              </tr>
            </thead>
            <tbody>
              {items.map((it) => (
                <tr key={it.id} className="border-t border-accanto-100">
                  <td className="py-2 pr-3 whitespace-nowrap">{fmtDate(it.createdAt)}</td>
                  <td className="py-2 pr-3">{it.function}</td>
                  <td className="py-2 pr-3">{it.verdict}</td>
                  <td className="py-2 pr-3">{it.feedback ?? '—'}</td>
                  <td className="py-2 pr-3 text-accanto-500">{it.model}</td>
                  <td className="py-2">
                    <button type="button" className="text-xs text-accanto-700 hover:underline" onClick={() => open(it.id)}>
                      {t('ai.history.open')}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {totalPages > 1 && (
        <div className="mt-4 flex items-center gap-2 text-sm">
          <button type="button" disabled={page <= 1}
            onClick={() => setParams({ page: String(page - 1) })}
            className="text-accanto-700 disabled:text-accanto-300">←</button>
          <span>{page} / {totalPages}</span>
          <button type="button" disabled={page >= totalPages}
            onClick={() => setParams({ page: String(page + 1) })}
            className="text-accanto-700 disabled:text-accanto-300">→</button>
        </div>
      )}

      {selected && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center p-4 z-40"
          role="dialog" aria-modal="true" onClick={() => setSelected(null)}>
          <div className="bg-white rounded-lg shadow-lg max-w-2xl w-full max-h-[80vh] overflow-auto p-4"
            onClick={(e) => e.stopPropagation()}>
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-medium">{selected.function}</h2>
              <button type="button" className="text-accanto-500" onClick={() => setSelected(null)}>×</button>
            </div>
            <p className="text-xs text-accanto-500 mt-1">
              {fmtDate(selected.createdAt)} · {selected.model} · {selected.verdict}
              {selected.cacheHit && ' · cache'}
            </p>
            <h3 className="text-sm font-medium mt-4">{t('ai.history.input')}</h3>
            <pre className="text-xs bg-accanto-50 border border-accanto-200 rounded p-2 mt-1 whitespace-pre-wrap break-words">{selected.input}</pre>
            <h3 className="text-sm font-medium mt-4">{t('ai.history.output')}</h3>
            <pre className="text-xs bg-accanto-50 border border-accanto-200 rounded p-2 mt-1 whitespace-pre-wrap break-words">{selected.output}</pre>
          </div>
        </div>
      )}
      {detailLoading && <div className="fixed bottom-4 right-4 text-xs text-accanto-500">…</div>}
    </main>
  );
}
