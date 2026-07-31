import { useCallback, useEffect, useState } from 'react';
import { listOperations } from '../api/endpoints';
import { DataTable, ErrorBox, Pagination, StatusBadge, formatDate } from '../components/ui';
import { OperationListResponse } from '../types';

export default function OperationsPage() {
  const [page, setPage] = useState(1);
  const [data, setData] = useState<OperationListResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  const pageSize = 20;

  const load = useCallback(async () => {
    setError(null);
    try {
      setData(await listOperations({ page, pageSize }));
    } catch {
      setError('Could not load operations.');
    }
  }, [page]);

  useEffect(() => { load(); }, [load]);

  return (
    <div className="space-y-4">
      <h1 className="text-lg font-semibold text-accanto-900">Operations</h1>
      <ErrorBox message={error} />

      <DataTable head={['Created at', 'Operation type', 'Target user', 'Status', 'Reason', 'Completed at', 'Error']}>
        {data?.items.map((op) => (
          <tr key={op.id} className="border-b border-accanto-100 last:border-0">
            <td className="px-4 py-2 text-accanto-600">{formatDate(op.createdAt)}</td>
            <td className="px-4 py-2 font-medium text-accanto-900">{op.operationType}</td>
            <td className="px-4 py-2 text-accanto-500">{op.targetUserId ?? '—'}</td>
            <td className="px-4 py-2"><StatusBadge status={op.status} /></td>
            <td className="px-4 py-2 text-accanto-600">{op.reason}</td>
            <td className="px-4 py-2 text-accanto-600">{formatDate(op.completedAt)}</td>
            <td className="px-4 py-2 text-accanto-500">{op.errorMessage ?? '—'}</td>
          </tr>
        ))}
        {data?.items.length === 0 && (
          <tr><td colSpan={7} className="px-4 py-6 text-center text-accanto-500">No operations.</td></tr>
        )}
      </DataTable>

      {data && <Pagination page={data.page} pageSize={data.pageSize} total={data.total} onPage={setPage} />}
    </div>
  );
}
