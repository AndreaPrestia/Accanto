import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { getSystemHealth, listOperations, listUsers } from '../api/endpoints';
import { ErrorBox, StatusBadge, formatBytes, formatDate } from '../components/ui';
import { Operation, SystemHealth, UserListResponse } from '../types';

export default function DashboardPage() {
  const [users, setUsers] = useState<UserListResponse | null>(null);
  const [disabled, setDisabled] = useState<UserListResponse | null>(null);
  const [operations, setOperations] = useState<Operation[]>([]);
  const [health, setHealth] = useState<SystemHealth | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [u, d, ops, h] = await Promise.all([
          listUsers({ page: 1, pageSize: 1 }),
          listUsers({ disabled: true, page: 1, pageSize: 1 }),
          listOperations({ page: 1, pageSize: 5 }),
          getSystemHealth()
        ]);
        if (cancelled) return;
        setUsers(u);
        setDisabled(d);
        setOperations(ops.items);
        setHealth(h);
      } catch {
        if (!cancelled) setError('Some dashboard data could not be loaded.');
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const totalStorage = null; // aggregated storage requires a dedicated metric; omitted in v0.1 (non-invasive).

  return (
    <div className="space-y-6">
      <h1 className="text-lg font-semibold text-accanto-900">Dashboard</h1>
      <ErrorBox message={error} />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="card">
          <div className="label">Total users</div>
          <div className="text-2xl font-semibold text-accanto-900">{users?.total ?? '—'}</div>
        </div>
        <div className="card">
          <div className="label">Disabled users</div>
          <div className="text-2xl font-semibold text-accanto-900">{disabled?.total ?? '—'}</div>
        </div>
        <div className="card">
          <div className="label">Total storage used</div>
          <div className="text-2xl font-semibold text-accanto-900">{totalStorage ?? '—'}</div>
        </div>
        <div className="card">
          <div className="label">System</div>
          <div className="mt-1 flex flex-col gap-1 text-sm">
            <span>Admin API {health ? <StatusBadge status={health.adminApi} /> : '—'}</span>
            <span>Admin DB {health ? <StatusBadge status={health.adminDb} /> : '—'}</span>
            <span>Public API (internal) {health ? <StatusBadge status={health.publicApiInternal} /> : '—'}</span>
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
              <li key={op.id} className="flex items-center justify-between py-2 text-sm">
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
        Last health check: {health ? formatDate(health.checkedAt) : '—'} · {formatBytes(0)} metrics are non-invasive.
      </p>
    </div>
  );
}
