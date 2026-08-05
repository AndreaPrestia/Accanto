import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getStats, getSystemHealth, listOperations } from '../api/endpoints';
import { ErrorBox, StatusBadge, formatBytes, formatDate } from '../components/ui';
import { AdminStats, Operation, SystemHealth } from '../types';

export default function DashboardPage() {
  const [stats, setStats] = useState<AdminStats | null>(null);
  const [operations, setOperations] = useState<Operation[]>([]);
  const [health, setHealth] = useState<SystemHealth | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [s, ops, h] = await Promise.all([
          getStats(),
          listOperations({ page: 1, pageSize: 5 }),
          getSystemHealth()
        ]);
        if (cancelled) return;
        setStats(s);
        setOperations(ops.items);
        setHealth(h);
      } catch {
        if (!cancelled) setError('Some dashboard data could not be loaded.');
      }
    })();
    return () => { cancelled = true; };
  }, []);

  return (
    <div className="space-y-6">
      <h1 className="text-lg font-semibold text-accanto-900">Dashboard</h1>
      <ErrorBox message={error} />

      <div className="grid grid-cols-2 gap-3 sm:gap-4 lg:grid-cols-4">
        <div className="card">
          <div className="label">Total users</div>
          <div className="text-2xl font-semibold text-accanto-900">{stats?.totalUsers ?? '—'}</div>
        </div>
        <div className="card">
          <div className="label">Disabled users</div>
          <div className="text-2xl font-semibold text-accanto-900">{stats?.disabledUsers ?? '—'}</div>
        </div>
        <div className="card">
          <div className="label">Total storage used</div>
          <div className="text-2xl font-semibold text-accanto-900">
            {stats ? formatBytes(stats.totalStorageBytes) : '—'}
          </div>
        </div>
        <div className="card">
          <div className="label">Documents</div>
          <div className="text-2xl font-semibold text-accanto-900">{stats?.totalDocuments ?? '—'}</div>
        </div>
      </div>

      <div className="card">
        <div className="label mb-2">System</div>
        <div className="grid grid-cols-1 gap-2 text-sm sm:grid-cols-3">
          <div className="flex items-center justify-between sm:block">
            <span className="text-accanto-500">Admin API</span>
            <div className="mt-0.5">{health ? <StatusBadge status={health.adminApi} /> : '—'}</div>
          </div>
          <div className="flex items-center justify-between sm:block">
            <span className="text-accanto-500">Admin DB</span>
            <div className="mt-0.5">{health ? <StatusBadge status={health.adminDb} /> : '—'}</div>
          </div>
          <div className="flex items-center justify-between sm:block">
            <span className="text-accanto-500">Public API (internal)</span>
            <div className="mt-0.5">{health ? <StatusBadge status={health.publicApiInternal} /> : '—'}</div>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-sm font-semibold text-accanto-900">Recent operations</h2>
          <Link to="/operations" className="text-sm text-accanto-600 hover:underline">View all</Link>
        </div>
        {operations.length === 0 ? (
          <div className="text-sm text-accanto-500">No operations yet.</div>
        ) : (
          <ul className="divide-y divide-accanto-100">
            {operations.map((op) => (
              <li
                key={op.id}
                className="flex flex-col gap-1 py-2 text-sm sm:flex-row sm:items-center sm:justify-between"
              >
                <div>
                  <span className="font-medium text-accanto-900">{op.operationType}</span>
                  <span className="ml-2 text-accanto-500">{formatDate(op.createdAt)}</span>
                </div>
                <StatusBadge status={op.status} />
              </li>
            ))}
          </ul>
        )}
      </div>

      <p className="text-xs text-accanto-500">
        Last health check: {health ? formatDate(health.checkedAt) : '—'}
      </p>
    </div>
  );
}
