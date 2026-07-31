import { useCallback, useEffect, useState } from 'react';
import { getSystemHealth } from '../api/endpoints';
import { ErrorBox, StatusBadge, formatDate } from '../components/ui';
import { SystemHealth } from '../types';

export default function SystemPage() {
  const [health, setHealth] = useState<SystemHealth | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

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

  useEffect(() => { load(); }, [load]);

  const cards: Array<{ label: string; status?: string }> = [
    { label: 'Admin API', status: health?.adminApi },
    { label: 'Admin DB', status: health?.adminDb },
    { label: 'Public API (internal)', status: health?.publicApiInternal }
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold text-accanto-900">System</h1>
        <button className="btn-ghost" onClick={load} disabled={loading}>
          {loading ? 'Refreshing…' : 'Refresh'}
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

      <div className="card">
        <h2 className="mb-2 text-sm font-semibold text-accanto-900">Technical logs</h2>
        <p className="text-sm text-accanto-500">
          Technical logs are not exposed in Admin v0.1. Only non-sensitive, filtered technical
          signals are surfaced via health checks above. No request/response bodies, user content,
          or filenames are shown.
        </p>
      </div>

      <p className="text-xs text-accanto-500">Last checked: {health ? formatDate(health.checkedAt) : '—'}</p>
    </div>
  );
}
