import { useEffect, useState } from 'react';
import { extractError } from '../api/client';
import { getExistingSubscription, isPushSupported, subscribeToPush, unsubscribeFromPush } from '../api/push';

type Status = 'unknown' | 'unsupported' | 'denied' | 'unsubscribed' | 'subscribed';

export default function PushNotificationsSection() {
  const [status, setStatus] = useState<Status>('unknown');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [msg, setMsg] = useState<string | null>(null);

  useEffect(() => {
    void refresh();
  }, []);

  async function refresh() {
    if (!isPushSupported()) {
      setStatus('unsupported');
      return;
    }
    if (Notification.permission === 'denied') {
      setStatus('denied');
      return;
    }
    const sub = await getExistingSubscription();
    setStatus(sub ? 'subscribed' : 'unsubscribed');
  }

  async function enable() {
    setBusy(true);
    setError(null);
    setMsg(null);
    try {
      await subscribeToPush();
      setStatus('subscribed');
      setMsg('Notifiche attivate su questo dispositivo.');
    } catch (e) {
      setError(extractError(e));
      await refresh();
    } finally {
      setBusy(false);
    }
  }

  async function disable() {
    setBusy(true);
    setError(null);
    setMsg(null);
    try {
      await unsubscribeFromPush();
      setStatus('unsubscribed');
      setMsg('Notifiche disattivate su questo dispositivo.');
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="space-y-3 border-t border-accanto-100 pt-6">
      <h2 className="text-base font-semibold text-accanto-900">Notifiche push</h2>
      <p className="text-sm text-accanto-700">
        Ricevi un avviso quando un altro membro di un cerchio aggiunge una voce al diario.
        Le notifiche sono legate a questo dispositivo e a questo browser.
      </p>

      {status === 'unsupported' && (
        <p className="text-sm text-accanto-500">Questo browser non supporta le notifiche push.</p>
      )}
      {status === 'denied' && (
        <p className="text-sm text-accanto-500">
          Hai negato il permesso per le notifiche. Per riattivarle, modifica le impostazioni del browser per questo sito.
        </p>
      )}

      {error && <p className="text-sm text-red-700">{error}</p>}
      {msg && <p className="text-sm text-green-700">{msg}</p>}

      {status === 'unsubscribed' && (
        <button
          onClick={enable}
          disabled={busy}
          className="w-full sm:w-auto px-4 py-2 rounded-lg bg-accanto-700 text-white disabled:opacity-60"
        >
          {busy ? 'Attivazione\u2026' : 'Attiva le notifiche'}
        </button>
      )}
      {status === 'subscribed' && (
        <button
          onClick={disable}
          disabled={busy}
          className="w-full sm:w-auto px-4 py-2 rounded-lg border border-accanto-300 text-accanto-700 disabled:opacity-60"
        >
          {busy ? 'Disattivazione\u2026' : 'Disattiva le notifiche'}
        </button>
      )}
    </section>
  );
}
