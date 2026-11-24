import { useCallback, useEffect, useState } from 'react';
import { getSystemHealth, listTechnicalLogs } from '../api/endpoints';
import { useAuth } from '../auth/AuthContext';
import { DataTable, ErrorBox, StatusBadge, formatDate } from '../components/ui';
import { SystemHealth, TechnicalLogListResponse } from '../types';

export default function SystemPage() {
  const { hasRole } = useAuth();
  const canViewTechnicalLogs = hasRole('Owner', 'SecurityAuditor');
  const [health, setHealth] = useState<SystemHealth | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [technicalLogs, setTechnicalLogs] = useState<TechnicalLogListResponse | null>(null);
  const [logsError, setLogsError] = useState<string | null>(null);
  const [logsLoading, setLogsLoading] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setHealth(await getSystemHealth());
    } catch {
      setError('Could not load system health.');
    } finally {
      setLoading(false);
    }
  }, []);

  const loadTechnicalLogs = useCallback(async () => {
    setLogsLoading(true);
    setLogsError(null);
    try {
      setTechnicalLogs(await listTechnicalLogs());
    } catch {
      setLogsError('Could not load technical logs.');
    } finally {
      setLogsLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);
  useEffect(() => {
    if (canViewTechnicalLogs) loadTechnicalLogs();
  }, [canViewTechnicalLogs, loadTechnicalLogs]);

  const cards: Array<{ label: string; status?: string }> = [
    { label: 'Admin API', status: health?.adminApi },
    { label: 'Admin DB', status: health?.adminDb },
    { label: 'Public API (internal)', status: health?.publicApiInternal }
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold text-accanto-900">System</h1>
        <button className="btn-ghost" onClick={() => { load(); if (canViewTechnicalLogs) loadTechnicalLogs(); }} disabled={loading || logsLoading}>
          {loading || logsLoading ? 'Refreshing…' : 'Refresh'}
        </button>
      </div>

      <ErrorBox message={error} />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        {cards.map((c) => (
          <div key={c.label} className="card">
            <div className="label">{c.label}</div>
            <div className="mt-1">{c.status ? <StatusBadge status={c.status} /> : '—'}</div>
          </div>
        ))}
      </div>

      {canViewTechnicalLogs && (
        <section className="space-y-3">
          <div>
            <h2 className="text-sm font-semibold text-accanto-900">Technical logs</h2>
            <p className="mt-1 text-xs text-accanto-500">
              Redacted API errors from this process only. Up to 200 events are kept in memory and cleared on restart; exception details and request data are never shown.
            </p>
          </div>
          <ErrorBox message={logsError} />
          <DataTable head={['Time', 'Level', 'Component', 'Event', 'Code']}>
            {technicalLogs?.items.map((entry, index) => (
              <tr key={`${entry.createdAt}-${index}`} className="border-b border-accanto-100 last:border-0">
                <td className="px-4 py-2 text-accanto-600">{formatDate(entry.createdAt)}</td>
                <td className="px-4 py-2"><StatusBadge status={entry.level} /></td>
                <td className="px-4 py-2 text-accanto-600">{entry.component}</td>
                <td className="px-4 py-2 text-accanto-900">{entry.event}</td>
                <td className="px-4 py-2 font-mono text-xs text-accanto-500">{entry.code}</td>
              </tr>
            ))}
            {technicalLogs?.items.length === 0 && (
              <tr><td colSpan={5} className="px-4 py-6 text-center text-accanto-500">No recent API errors.</td></tr>
            )}
          </DataTable>
          {technicalLogs && (
            <p className="text-xs text-accanto-500">
              Showing {technicalLogs.items.length} of {technicalLogs.retainedCount} retained events.
            </p>
          )}
        </section>
      )}

      <p className="text-xs text-accanto-500">Last checked: {health ? formatDate(health.checkedAt) : '—'}</p>
    </div>
  );
}
