import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { AxiosError } from 'axios';
import { disableUser, enableUser, getUser, revokeUserSessions, startUserDeletion } from '../api/endpoints';
import ReasonModal from '../components/ReasonModal';
import { useAuth } from '../auth/AuthContext';
import { ErrorBox, StatusBadge, formatBytes, formatDate } from '../components/ui';
import { UserMetadata } from '../types';

type ActionKind = 'disable' | 'enable' | 'revoke' | 'delete';

const ACTION_CONFIG: Record<ActionKind, { title: string; label: string; danger: boolean; run: (id: string, reason: string) => Promise<unknown> }> = {
  disable: { title: 'Disable account', label: 'Disable account', danger: true, run: disableUser },
  enable: { title: 'Enable account', label: 'Enable account', danger: false, run: enableUser },
  revoke: { title: 'Revoke sessions', label: 'Revoke sessions', danger: false, run: revokeUserSessions },
  delete: { title: 'Start data deletion', label: 'Start deletion', danger: true, run: startUserDeletion }
};

function mapError(err: unknown): string {
  const ax = err as AxiosError;
  const status = ax?.response?.status;
  if (status === 403) return 'Your role is not allowed to perform this action.';
  if (status === 422) return 'A valid reason is required.';
  if (status === 404) return 'User not found.';
  return 'The operation failed. Please try again.';
}

export default function UserDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { canMutate } = useAuth();
  const [user, setUser] = useState<UserMetadata | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [action, setAction] = useState<ActionKind | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);

  const load = useCallback(async () => {
    if (!id) return;
    try {
      setUser(await getUser(id));
    } catch {
      setError('Could not load user.');
    }
  }, [id]);

  useEffect(() => { load(); }, [load]);

  const confirm = async (reason: string) => {
    if (!id || !action) return;
    setActionLoading(true);
    setActionError(null);
    try {
      await ACTION_CONFIG[action].run(id, reason);
      setAction(null);
      await load();
    } catch (err) {
      setActionError(mapError(err));
    } finally {
      setActionLoading(false);
    }
  };

  if (error) return <ErrorBox message={error} />;
  if (!user) return <div className="text-accanto-500">Loading…</div>;

  const rows: Array<[string, string]> = [
    ['User ID', user.userId],
    ['Email', user.email],
    ['Display name', user.displayName],
    ['Created at', formatDate(user.createdAt)],
    ['Status', user.accountStatus],
    ['Care circles', String(user.careCircleCount)],
    ['Documents', String(user.documentsCount)],
    ['Storage used', formatBytes(user.storageUsedBytes)],
    ['Timeline entries', String(user.timelineEntryCount)],
    ['Disabled at', formatDate(user.disabledAt)],
    ['Disabled reason', user.disabledReason ?? '—']
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold text-accanto-900">User detail</h1>
        <Link to="/users" className="text-sm text-accanto-600 hover:underline">← Back to users</Link>
      </div>

      <div className="card">
        <div className="mb-4 flex items-center justify-between">
          <div className="text-base font-medium text-accanto-900">{user.email}</div>
          <StatusBadge status={user.accountStatus} />
        </div>
        <dl className="grid grid-cols-1 gap-x-8 gap-y-3 sm:grid-cols-2">
          {rows.map(([k, v]) => (
            <div key={k}>
              <dt className="label">{k}</dt>
              <dd className="text-sm text-accanto-900">{v}</dd>
            </div>
          ))}
        </dl>
      </div>

      <div className="card">
        <h2 className="mb-1 text-sm font-semibold text-accanto-900">Account actions</h2>
        <p className="mb-4 text-xs text-accanto-500">
          Every action requires a reason and is recorded in the admin audit log. These actions do not
          read or modify the user's private content.
        </p>
        {canMutate ? (
          <div className="flex flex-wrap gap-2">
            {user.isDisabled ? (
              <button className="btn-primary" onClick={() => setAction('enable')}>Enable account</button>
            ) : (
              <button className="btn-danger" onClick={() => setAction('disable')}>Disable account</button>
            )}
            <button className="btn-ghost" onClick={() => setAction('revoke')}>Revoke sessions</button>
            {!user.isDisabled && user.accountStatus !== 'Erased' && (
              <button className="btn-danger" onClick={() => setAction('delete')}>Start data deletion</button>
            )}
          </div>
        ) : (
          <p className="text-sm text-accanto-500">Your role has read-only access to user accounts.</p>
        )}
      </div>

      {action && (
        <ReasonModal
          open
          title={ACTION_CONFIG[action].title}
          confirmLabel={ACTION_CONFIG[action].label}
          danger={ACTION_CONFIG[action].danger}
          loading={actionLoading}
          error={actionError}
          onConfirm={confirm}
          onCancel={() => { setAction(null); setActionError(null); }}
        />
      )}
    </div>
  );
}
