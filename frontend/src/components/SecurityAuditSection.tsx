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
    <section className="security-audit-section">
      <h2>{t('account.securityAudit.title')}</h2>
      <p className="muted">{t('account.securityAudit.hint')}</p>

      {error && <p className="error">{error}</p>}

      {data && data.items.length === 0 && (
        <p className="muted">{t('account.securityAudit.empty')}</p>
      )}

      {data && data.items.length > 0 && (
        <table className="audit-table">
          <thead>
            <tr>
              <th>{t('account.securityAudit.colWhen')}</th>
              <th>{t('account.securityAudit.colEvent')}</th>
              <th>{t('account.securityAudit.colDetails')}</th>
              <th>{t('account.securityAudit.colWhere')}</th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((e) => (
              <tr key={e.id}>
                <td>{fmt(e.timestamp)}</td>
                <td>{eventLabel(e.eventType)}</td>
                <td>{e.summary ?? ''}</td>
                <td>
                  {e.ipAddress ?? ''}
                  {e.userAgent ? (
                    <>
                      <br />
                      <small className="muted">{e.userAgent}</small>
                    </>
                  ) : null}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div className="pager">
        <button type="button" disabled={!hasPrev || busy} onClick={() => load(Math.max(0, skip - PAGE_SIZE))}>
          {t('common.previous', { defaultValue: '← Precedente' })}
        </button>
        <button type="button" disabled={!hasNext || busy} onClick={() => load(skip + PAGE_SIZE)}>
          {t('common.next', { defaultValue: 'Successivo →' })}
        </button>
      </div>
    </section>
  );
}
