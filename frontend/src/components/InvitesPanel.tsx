import { useEffect, useState } from 'react';
import { api, extractError } from '../api/client';
import { CareCircleInvite, CareCircleRole, CreateInviteRequest, RoleLabel } from '../types';

interface Props {
  circleId: string;
}

export default function InvitesPanel({ circleId }: Props) {
  const [invites, setInvites] = useState<CareCircleInvite[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [role, setRole] = useState<CareCircleRole>('Caregiver');
  const [expiresInDays, setExpiresInDays] = useState<number>(7);
  const [maxUses, setMaxUses] = useState<number>(1);
  const [copied, setCopied] = useState<string | null>(null);

  const load = async () => {
    try {
      const { data } = await api.get<CareCircleInvite[]>(`/care-circles/${circleId}/invites`);
      setInvites(data);
    } catch (e) {
      setError(extractError(e));
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [circleId]);

  const create = async () => {
    setCreating(true);
    setError(null);
    try {
      const body: CreateInviteRequest = { role, expiresInDays, maxUses };
      const { data } = await api.post<CareCircleInvite>(`/care-circles/${circleId}/invites`, body);
      setInvites((prev) => (prev ? [data, ...prev] : [data]));
    } catch (e) {
      setError(extractError(e));
    } finally {
      setCreating(false);
    }
  };

  const revoke = async (inviteId: string) => {
    if (!confirm('Vuoi revocare questo link di invito? Chi non lo ha ancora usato non potrà più accedere.')) return;
    try {
      await api.delete(`/care-circles/${circleId}/invites/${inviteId}`);
      await load();
    } catch (e) {
      setError(extractError(e));
    }
  };

  const inviteUrl = (token: string) => `${location.origin}/invite/${token}`;

  const copy = async (token: string) => {
    try {
      await navigator.clipboard.writeText(inviteUrl(token));
      setCopied(token);
      setTimeout(() => setCopied((cur) => (cur === token ? null : cur)), 2000);
    } catch {
      setError('Non riesco a copiare negli appunti, copialo a mano.');
    }
  };

  return (
    <section className="card mt-6">
      <h2 className="font-medium">Invita altre persone</h2>
      <p className="text-sm text-accanto-500 mt-1">
        Crea un link da condividere con chi vuoi far entrare nel cerchio.
        Tu sola/o decidi il ruolo e quanto a lungo il link resta valido.
      </p>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mt-4">
        <div>
          <label className="label">Ruolo</label>
          <select className="input" value={role} onChange={(e) => setRole(e.target.value as CareCircleRole)}>
            <option value="Caregiver">{RoleLabel.Caregiver}</option>
            <option value="Viewer">{RoleLabel.Viewer}</option>
          </select>
        </div>
        <div>
          <label className="label">Scadenza (giorni)</label>
          <input
            className="input"
            type="number"
            min={1}
            max={90}
            value={expiresInDays}
            onChange={(e) => setExpiresInDays(Math.max(1, Math.min(90, Number(e.target.value) || 1)))}
          />
        </div>
        <div>
          <label className="label">Numero massimo di usi</label>
          <input
            className="input"
            type="number"
            min={1}
            max={50}
            value={maxUses}
            onChange={(e) => setMaxUses(Math.max(1, Math.min(50, Number(e.target.value) || 1)))}
          />
        </div>
      </div>

      <button className="btn-primary mt-4" onClick={create} disabled={creating}>
        {creating ? 'Creazione…' : 'Crea link di invito'}
      </button>

      {error && (
        <div className="mt-3 text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2">{error}</div>
      )}

      <div className="mt-6 space-y-3">
        {invites === null && <p className="text-sm text-accanto-500">Caricamento…</p>}
        {invites && invites.length === 0 && (
          <p className="text-sm text-accanto-500">Nessun invito attivo.</p>
        )}
        {invites && invites.map((i) => (
          <InviteRow
            key={i.id}
            invite={i}
            url={inviteUrl(i.token)}
            copied={copied === i.token}
            onCopy={() => copy(i.token)}
            onRevoke={() => revoke(i.id)}
          />
        ))}
      </div>
    </section>
  );
}

function InviteRow({ invite, url, copied, onCopy, onRevoke }: {
  invite: CareCircleInvite;
  url: string;
  copied: boolean;
  onCopy: () => void;
  onRevoke: () => void;
}) {
  const status = invite.revokedAt
    ? 'Revocato'
    : !invite.isActive
      ? (new Date(invite.expiresAt) <= new Date() ? 'Scaduto' : 'Esaurito')
      : 'Attivo';
  const expires = new Date(invite.expiresAt).toLocaleDateString('it-IT', { day: '2-digit', month: 'long', year: 'numeric' });

  return (
    <div className="border border-accanto-100 rounded-md p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="text-sm">
          <strong>{RoleLabel[invite.role]}</strong> • scade il {expires} •{' '}
          {invite.usedCount}/{invite.maxUses} usi
        </div>
        <span
          className={`text-xs px-2 py-0.5 rounded-full ${
            status === 'Attivo'
              ? 'bg-accanto-50 text-accanto-700'
              : 'bg-accanto-100 text-accanto-500'
          }`}
        >
          {status}
        </span>
      </div>

      {invite.isActive && (
        <>
          <div className="mt-2 break-all text-xs text-accanto-500 bg-accanto-50 rounded-md px-2 py-1 font-mono">
            {url}
          </div>
          <div className="mt-2 flex flex-wrap gap-2">
            <button className="btn-ghost" onClick={onCopy}>{copied ? 'Copiato!' : 'Copia link'}</button>
            <button className="btn-ghost" onClick={onRevoke}>Revoca</button>
          </div>
        </>
      )}
    </div>
  );
}
