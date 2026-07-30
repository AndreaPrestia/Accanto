import { FormEvent, useCallback, useEffect, useState } from 'react';
import { listAuditLogs } from '../api/endpoints';
import { DataTable, ErrorBox, Pagination, formatDate } from '../components/ui';
import { AuditLogListResponse } from '../types';

export default function AuditLogsPage() {
  const [action, setAction] = useState('');
  const [targetType, setTargetType] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<AuditLogListResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const pageSize = 20;

  const load = useCallback(async () => {
    setError(null);
    try {
      setData(await listAuditLogs({
        action: action || undefined,
        targetType: targetType || undefined,
        from: from ? new Date(from).toISOString() : undefined,
        to: to ? new Date(to).toISOString() : undefined,
        page,
        pageSize
      }));
    } catch {
      setError('Could not load audit logs.');
    }
  }, [action, targetType, from, to, page]);

  useEffect(() => { load(); }, [load]);

  const apply = (e: FormEvent) => {
    e.preventDefault();
    setPage(1);
    load();
  };

  return (
    <div className="space-y-4">
      <h1 className="text-lg font-semibold text-accanto-900">Audit logs</h1>

      <form onSubmit={apply} className="flex flex-wrap items-end gap-3">
        <div>
          <label className="label">Action</label>
          <input className="input" value={action} onChange={(e) => setAction(e.target.value)} placeholder="User.Disable" />
        </div>
        <div>
          <label className="label">Target type</label>
          <input className="input" value={targetType} onChange={(e) => setTargetType(e.target.value)} placeholder="User" />
        </div>
        <div>
          <label className="label">From</label>
          <input type="datetime-local" className="input" value={from} onChange={(e) => setFrom(e.target.value)} />
        </div>
        <div>
          <label className="label">To</label>
          <input type="datetime-local" className="input" value={to} onChange={(e) => setTo(e.target.value)} />
        </div>
        <button type="submit" className="btn-ghost">Apply</button>
      </form>

      <ErrorBox message={error} />

      <DataTable head={['Created at', 'Admin', 'Action', 'Target type', 'Target id', 'Reason', 'IP', 'User agent']}>
        {data?.items.map((a) => (
          <tr key={a.id} className="border-b border-accanto-100 last:border-0">
            <td className="px-4 py-2 text-accanto-600">{formatDate(a.createdAt)}</td>
            <td className="px-4 py-2">{a.adminEmail ?? '—'}</td>
            <td className="px-4 py-2 font-medium text-accanto-900">{a.action}</td>
            <td className="px-4 py-2">{a.targetType}</td>
            <td className="px-4 py-2 text-accanto-500">{a.targetId ?? '—'}</td>
            <td className="px-4 py-2 text-accanto-600">{a.reason ?? '—'}</td>
            <td className="px-4 py-2 text-accanto-500">{a.ipAddress ?? '—'}</td>
            <td className="px-4 py-2 text-accanto-500">{a.userAgent ?? '—'}</td>
          </tr>
        ))}
        {data?.items.length === 0 && (
          <tr><td colSpan={8} className="px-4 py-6 text-center text-accanto-500">No audit entries.</td></tr>
        )}
      </DataTable>

      {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onPage={setPage} />}
    </div>
  );
}
