import { useEffect, useState } from 'react';
import { api, extractError } from '../api/client';

type Topic = 'TimelineEntryCreated' | 'SharedUpdateCreated' | 'DoctorQuestionAnswered' | 'InviteAccepted';

type Pref = { topic: Topic; emailEnabled: boolean };

const TOPIC_LABEL: Record<Topic, string> = {
  TimelineEntryCreated: 'Nuove voci nel diario',
  SharedUpdateCreated: 'Nuovi aggiornamenti condivisi',
  DoctorQuestionAnswered: 'Risposte alle domande al medico',
  InviteAccepted: 'Nuove persone entrate in un cerchio'
};

export default function NotificationPreferencesSection() {
  const [prefs, setPrefs] = useState<Pref[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [msg, setMsg] = useState<string | null>(null);

  useEffect(() => {
    let cancel = false;
    (async () => {
      try {
        const res = await api.get<Pref[]>('/account/notification-preferences');
        if (!cancel) setPrefs(res.data);
      } catch (e) {
        if (!cancel) setError(extractError(e));
      } finally {
        if (!cancel) setLoading(false);
      }
    })();
    return () => {
      cancel = true;
    };
  }, []);

  function toggle(topic: Topic) {
    setMsg(null);
    setPrefs((cur) =>
      (cur ?? []).map((p) => (p.topic === topic ? { ...p, emailEnabled: !p.emailEnabled } : p))
    );
  }

  async function save() {
    if (!prefs) return;
    setSaving(true);
    setError(null);
    setMsg(null);
    try {
      const res = await api.put<Pref[]>('/account/notification-preferences', {
        preferences: prefs
      });
      setPrefs(res.data);
      setMsg('Preferenze salvate.');
    } catch (e) {
      setError(extractError(e));
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <section className="space-y-3 border-t border-accanto-100 pt-6">
        <h2 className="text-base font-semibold text-accanto-900">Notifiche email</h2>
        <p className="text-sm text-accanto-500">Caricamento\u2026</p>
      </section>
    );
  }

  return (
    <section className="space-y-3 border-t border-accanto-100 pt-6">
      <h2 className="text-base font-semibold text-accanto-900">Notifiche email</h2>
      <p className="text-sm text-accanto-700">
        Scegli quali email vuoi ricevere. Le email di sicurezza (es. cambio password) vengono inviate sempre.
      </p>
      <ul className="space-y-2">
        {(prefs ?? []).map((p) => (
          <li key={p.topic} className="flex items-center justify-between gap-3 border border-accanto-100 rounded-lg px-3 py-2">
            <span className="text-sm text-accanto-800">{TOPIC_LABEL[p.topic]}</span>
            <label className="inline-flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={p.emailEnabled}
                onChange={() => toggle(p.topic)}
              />
              <span className="text-accanto-700">Email</span>
            </label>
          </li>
        ))}
      </ul>
      {error && <p className="text-sm text-red-700">{error}</p>}
      {msg && <p className="text-sm text-green-700">{msg}</p>}
      <button
        type="button"
        onClick={save}
        disabled={saving || !prefs}
        className="w-full sm:w-auto px-4 py-2 rounded-lg bg-accanto-700 text-white disabled:opacity-60"
      >
        {saving ? 'Salvataggio\u2026' : 'Salva preferenze'}
      </button>
    </section>
  );
}
