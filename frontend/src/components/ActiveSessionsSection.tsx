import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import { ActiveSession } from '../types';

const REFRESH_KEY = 'accanto.refreshToken';

function formatDate(value: string, locale: string): string {
  try {
    return new Date(value).toLocaleString(locale);
  } catch {
    return value;
  }
}

function shortenUserAgent(ua: string | null | undefined, fallback: string): string {
  if (!ua) return fallback;
  // Riduci stringhe UA prolisse a qualcosa di leggibile.
  const browser =
    /Edg\/([\d.]+)/.exec(ua)?.[0].replace('Edg/', 'Edge ') ||
    /Firefox\/([\d.]+)/.exec(ua)?.[0].replace('/', ' ') ||
    /Chrome\/([\d.]+)/.exec(ua)?.[0].replace('/', ' ') ||
    /Safari\/([\d.]+)/.exec(ua)?.[0].replace('/', ' ') ||
    ua.slice(0, 80);
  const os =
    /Windows NT [\d.]+/.exec(ua)?.[0] ||
    /Mac OS X [\d_.]+/.exec(ua)?.[0].replace(/_/g, '.') ||
    /Android [\d.]+/.exec(ua)?.[0] ||
    /iPhone OS [\d_]+/.exec(ua)?.[0].replace(/_/g, '.') ||
    /Linux/.exec(ua)?.[0] ||
    '';
  return [browser, os].filter(Boolean).join(' · ') || ua.slice(0, 80);
}

export default function ActiveSessionsSection() {
  const { t, i18n } = useTranslation();
  const [sessions, setSessions] = useState<ActiveSession[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [revoking, setRevoking] = useState<string | null>(null);

  async function load() {
    setError(null);
    setLoading(true);
    try {
      const refreshToken = localStorage.getItem(REFRESH_KEY) ?? '';
      const res = await api.get<ActiveSession[]>('/account/sessions', {
        params: refreshToken ? { current: refreshToken } : undefined
      });
      setSessions(res.data);
    } catch (e) {
      setError(extractError(e) || t('account.sessionsLoadError'));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function revoke(id: string) {
    setRevoking(id);
    setError(null);
    try {
      await api.delete(`/account/sessions/${id}`);
      setSessions((prev) => prev?.filter((s) => s.id !== id) ?? null);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setRevoking(null);
    }
  }

  const locale = i18n.language || 'it';

  return (
    <section className="space-y-3">
      <h2 className="text-base font-semibold text-accanto-900">{t('account.sessionsTitle')}</h2>
      <p className="text-sm text-accanto-500">{t('account.sessionsHint')}</p>
      {error && <p className="text-sm text-red-700">{error}</p>}
      {loading && <p className="text-sm text-accanto-500">…</p>}
      {!loading && sessions && sessions.length === 0 && (
        <p className="text-sm text-accanto-500">{t('account.sessionsEmpty')}</p>
      )}
      {!loading && sessions && sessions.length > 0 && (
        <ul className="space-y-2">
          {sessions.map((s) => (
            <li
              key={s.id}
              className="border border-accanto-200 rounded-lg p-3 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2"
            >
              <div className="text-sm">
                <div className="font-medium text-accanto-900">
                  {shortenUserAgent(s.userAgent, t('account.sessionsUnknownDevice'))}
                  {s.current && (
                    <span className="ml-2 inline-block rounded bg-green-100 text-green-800 px-2 py-0.5 text-xs">
                      {t('account.sessionsCurrent')}
                    </span>
                  )}
                </div>
                <div className="text-accanto-500 text-xs mt-1">
                  {t('account.sessionsCreatedAt')}: {formatDate(s.createdAt, locale)}
                  {' · '}
                  {t('account.sessionsExpiresAt')}: {formatDate(s.expiresAt, locale)}
                  {s.ipAddress ? ` · ${s.ipAddress}` : ''}
                </div>
              </div>
              {!s.current && (
                <button
                  type="button"
                  disabled={revoking === s.id}
                  onClick={() => revoke(s.id)}
                  className="text-sm rounded-lg border border-red-300 text-red-700 hover:bg-red-50 px-3 py-1.5 disabled:opacity-60"
                >
                  {revoking === s.id ? t('account.sessionsRevoking') : t('account.sessionsRevoke')}
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
