import { ReactNode } from 'react';

export function formatBytes(bytes: number): string {
  if (!bytes || bytes <= 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  const value = bytes / Math.pow(1024, i);
  return `${value.toFixed(value >= 10 || i === 0 ? 0 : 1)} ${units[i]}`;
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  const d = new Date(value);
  return isNaN(d.getTime()) ? '—' : d.toLocaleString();
}

export function StatusBadge({ status }: { status: string }) {
  const map: Record<string, string> = {
    Active: 'bg-green-100 text-green-800',
    Disabled: 'bg-amber-100 text-amber-800',
    Erased: 'bg-accanto-100 text-accanto-700',
    Completed: 'bg-green-100 text-green-800',
    Pending: 'bg-amber-100 text-amber-800',
    Failed: 'bg-red-100 text-red-800',
    Cancelled: 'bg-accanto-100 text-accanto-700',
    Healthy: 'bg-green-100 text-green-800',
    Degraded: 'bg-amber-100 text-amber-800',
    Down: 'bg-red-100 text-red-800'
  };
  const cls = map[status] ?? 'bg-accanto-100 text-accanto-700';
  return <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${cls}`}>{status}</span>;
}

export function DataTable({ head, children }: { head: ReactNode[]; children: ReactNode }) {
  return (
    <div className="card overflow-x-auto p-0">
      <table className="w-full text-left text-sm">
        <thead>
          <tr className="border-b border-accanto-200 text-xs uppercase tracking-wide text-accanto-500">
            {head.map((h, i) => (
              <th key={i} className="px-4 py-3 font-medium">{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>{children}</tbody>
      </table>
    </div>
  );
}

export function Pagination({
  page,
  pageSize,
  total,
  onPage
}: {
  page: number;
  pageSize: number;
  total: number;
  onPage: (page: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <div className="mt-4 flex items-center justify-between text-sm text-accanto-600">
      <span>
        Page {page} of {pages} · {total} total
      </span>
      <div className="flex gap-2">
        <button className="btn-ghost" disabled={page <= 1} onClick={() => onPage(page - 1)}>
          Previous
        </button>
        <button className="btn-ghost" disabled={page >= pages} onClick={() => onPage(page + 1)}>
          Next
        </button>
      </div>
    </div>
  );
}

export function ErrorBox({ message }: { message: string | null }) {
  if (!message) return null;
  return <div className="rounded-md bg-red-50 p-3 text-sm text-red-700">{message}</div>;
}
