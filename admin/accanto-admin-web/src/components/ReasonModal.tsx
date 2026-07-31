import { FormEvent, useEffect, useState } from 'react';

interface Props {
  open: boolean;
  title: string;
  description?: string;
  confirmLabel?: string;
  danger?: boolean;
  loading?: boolean;
  error?: string | null;
  onConfirm: (reason: string) => void;
  onCancel: () => void;
}

const MIN_REASON = 10;
const MAX_REASON = 500;

/**
 * Modale di conferma per OGNI azione mutativa admin. Richiede una reason
 * (obbligatoria, 10-500 char). L'azione viene registrata nell'audit log admin
 * e NON legge ne' modifica i contenuti privati dell'utente.
 */
export default function ReasonModal({
  open,
  title,
  description,
  confirmLabel = 'Confirm',
  danger,
  loading,
  error,
  onConfirm,
  onCancel
}: Props) {
  const [reason, setReason] = useState('');
  const [touched, setTouched] = useState(false);

  useEffect(() => {
    if (open) {
      setReason('');
      setTouched(false);
    }
  }, [open]);

  if (!open) return null;

  const trimmed = reason.trim();
  const valid = trimmed.length >= MIN_REASON && trimmed.length <= MAX_REASON;

  const submit = (e: FormEvent) => {
    e.preventDefault();
    setTouched(true);
    if (valid) onConfirm(trimmed);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" role="dialog" aria-modal="true">
      <form onSubmit={submit} className="card w-full max-w-md">
        <h2 className="text-base font-semibold text-accanto-900">{title}</h2>
        {description && <p className="mt-1 text-sm text-accanto-600">{description}</p>}

        <p className="mt-3 rounded-md bg-accanto-50 p-3 text-xs text-accanto-600">
          This action will be recorded in the admin audit log. It will not read or modify the
          user's private content.
        </p>

        <div className="mt-4">
          <label htmlFor="reason" className="label">Reason (required)</label>
          <textarea
            id="reason"
            className="input min-h-[96px]"
            value={reason}
            maxLength={MAX_REASON}
            onChange={(e) => setReason(e.target.value)}
            onBlur={() => setTouched(true)}
            placeholder="Why are you performing this action?"
          />
          <div className="mt-1 flex justify-between text-xs">
            <span className={touched && !valid ? 'text-red-600' : 'text-accanto-500'}>
              {touched && !valid
                ? `Reason required (${MIN_REASON}-${MAX_REASON} chars).`
                : 'Recorded in the admin audit log.'}
            </span>
            <span className="text-accanto-500">{trimmed.length}/{MAX_REASON}</span>
          </div>
        </div>

        {error && <div className="mt-3 rounded-md bg-red-50 p-2 text-sm text-red-700">{error}</div>}

        <div className="mt-5 flex justify-end gap-2">
          <button type="button" onClick={onCancel} className="btn-ghost" disabled={loading}>
            Cancel
          </button>
          <button
            type="submit"
            disabled={!valid || loading}
            className={danger ? 'btn-danger' : 'btn-primary'}
          >
            {loading ? 'Working…' : confirmLabel}
          </button>
        </div>
      </form>
    </div>
  );
}
