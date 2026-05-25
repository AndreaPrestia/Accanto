import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import { PagedResult, SecurityAuditEntry } from '../types';

const PAGE_SIZE = 20;

export default function SecurityAuditSection() {
  const { t, i18n } = useTranslation();
  const [skip, setSkip] = useState(0);
  const [data, setData] = useState<PagedResult<SecurityAuditEntry> | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async (newSkip = skip) => {
    setBusy(true);
    setError(null);
    try {
      const { data } = await api.get<PagedResult<SecurityAuditEntry>>('/account/security-audit', {
        params: { skip: newSkip, take: PAGE_SIZE }
      });
      setData(data);
      setSkip(newSkip);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  };

  useEffect(() => {
    load(0);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const fmt = (iso: string) => {
    try {
      return new Date(iso).toLocaleString(i18n.language);
    } catch {
      return iso;
    }
  };

  const eventLabel = (type: string) =>
    t(`account.securityAudit.events.${type}`, { defaultValue: type });

  const total = data?.total ?? 0;
  const hasPrev = skip > 0;
  const hasNext = data ? skip + data.items.length < total : false;

  return (
    <section className="space-y-3">
      <h2 className="text-base font-semibold text-accanto-900">{t('account.securityAudit.title')}</h2>
      <p className="text-sm text-accanto-500">{t('account.securityAudit.hint')}</p>

      {error && <p className="text-sm text-red-700">{error}</p>}

      {data && data.items.length === 0 && (
        <p className="text-sm text-accanto-500">{t('account.securityAudit.empty')}</p>
      )}

      {data && data.items.length > 0 && (
        <ul className="space-y-2">
          {data.items.map((e) => (
            <li key={e.id} className="card">
              <div className="flex items-baseline justify-between gap-2">
                <span className="text-sm font-medium text-accanto-900">{eventLabel(e.eventType)}</span>
                <span className="text-xs text-accanto-500 shrink-0">{fmt(e.timestamp)}</span>
              </div>
              {e.summary && (
                <p className="text-sm text-accanto-700 mt-1">{e.summary}</p>
              )}
              {(e.ipAddress || e.userAgent) && (
                <p className="text-xs text-accanto-500 mt-1 break-words">
                  {e.ipAddress}
                  {e.ipAddress && e.userAgent ? ' · ' : ''}
                  {e.userAgent}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}

      <div className="flex gap-2">
        <button
          type="button"
          disabled={!hasPrev || busy}
          onClick={() => load(Math.max(0, skip - PAGE_SIZE))}
          className="px-3 py-1.5 rounded-lg border border-accanto-200 text-sm text-accanto-700 disabled:opacity-50"
        >
          {t('common.previous', { defaultValue: '← Precedente' })}
        </button>
        <button
          type="button"
          disabled={!hasNext || busy}
          onClick={() => load(skip + PAGE_SIZE)}
          className="px-3 py-1.5 rounded-lg border border-accanto-200 text-sm text-accanto-700 disabled:opacity-50"
        >
          {t('common.next', { defaultValue: 'Successivo →' })}
        </button>
      </div>
    </section>
  );
}
