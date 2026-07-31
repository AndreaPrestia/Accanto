import { FormEvent, useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { listUsers } from '../api/endpoints';
import { DataTable, ErrorBox, Pagination, StatusBadge, formatBytes, formatDate } from '../components/ui';
import { UserListResponse } from '../types';

export default function UsersPage() {
  const [q, setQ] = useState('');
  const [status, setStatus] = useState<'all' | 'active' | 'disabled'>('all');
  const [page, setPage] = useState(1);
  const [data, setData] = useState<UserListResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const pageSize = 20;

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listUsers({
        q: q || undefined,
        disabled: status === 'all' ? undefined : status === 'disabled',
        page,
        pageSize
      });
      setData(res);
    } catch {
      setError('Could not load users.');
    } finally {
      setLoading(false);
    }
  }, [q, status, page]);

  useEffect(() => {
    const t = setTimeout(load, 250); // debounce search
    return () => clearTimeout(t);
  }, [load]);

  const onSearch = (e: FormEvent) => {
    e.preventDefault();
    setPage(1);
    load();
  };

  return (
    <div className="space-y-4">
      <h1 className="text-lg font-semibold text-accanto-900">Users</h1>

      <form onSubmit={onSearch} className="flex flex-wrap items-end gap-3">
        <div className="min-w-[220px] flex-1">
          <label className="label">Search email / display name</label>
          <input className="input" value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} placeholder="user@example.com" />
        </div>
        <div>
          <label className="label">Status</label>
          <select className="input" value={status} onChange={(e) => { setStatus(e.target.value as typeof status); setPage(1); }}>
            <option value="all">All</option>
            <option value="active">Active</option>
            <option value="disabled">Disabled</option>
          </select>
        </div>
      </form>

      <ErrorBox message={error} />

      <DataTable
        head={['Email', 'Display name', 'Created', 'Status', 'Care circles', 'Documents', 'Storage', '']}
      >
        {data?.items.map((u) => (
          <tr key={u.userId} className="border-b border-accanto-100 last:border-0 hover:bg-accanto-50">
            <td className="px-4 py-2 font-medium text-accanto-900">{u.email}</td>
            <td className="px-4 py-2">{u.displayName}</td>
            <td className="px-4 py-2 text-accanto-600">{formatDate(u.createdAt)}</td>
            <td className="px-4 py-2"><StatusBadge status={u.accountStatus} /></td>
            <td className="px-4 py-2 text-accanto-600">{u.careCircleCount}</td>
            <td className="px-4 py-2 text-accanto-600">{u.documentsCount}</td>
            <td className="px-4 py-2 text-accanto-600">{formatBytes(u.storageUsedBytes)}</td>
            <td className="px-4 py-2 text-right">
              <Link to={`/users/${u.userId}`} className="text-sm text-accanto-700 hover:underline">View</Link>
            </td>
          </tr>
        ))}
        {!loading && data?.items.length === 0 && (
          <tr><td colSpan={8} className="px-4 py-6 text-center text-accanto-500">No users found.</td></tr>
        )}
      </DataTable>

      {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onPage={setPage} />}
    </div>
  );
}
