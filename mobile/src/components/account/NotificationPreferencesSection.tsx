import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, Text, View } from 'react-native';
import Button from '../ui/Button';
import ErrorBanner from '../ui/ErrorBanner';
import { api, extractError } from '../../api/client';

type Topic =
  | 'TimelineEntryCreated'
  | 'SharedUpdateCreated'
  | 'DoctorQuestionAnswered'
  | 'InviteAccepted';

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
        const { data } = await api.get<Pref[]>(
          '/account/notification-preferences'
        );
        if (!cancel) setPrefs(data);
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

  const toggle = (topic: Topic) => {
    setMsg(null);
    setPrefs((cur) =>
      (cur ?? []).map((p) =>
        p.topic === topic ? { ...p, emailEnabled: !p.emailEnabled } : p
      )
    );
  };

  const save = async () => {
    if (!prefs) return;
    setSaving(true);
    setError(null);
    setMsg(null);
    try {
      const { data } = await api.put<Pref[]>(
        '/account/notification-preferences',
        { preferences: prefs }
      );
      setPrefs(data);
      setMsg('Preferenze salvate.');
    } catch (e) {
      setError(extractError(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <View className="gap-3 border-t border-accanto-100 pt-6">
      <Text className="text-base font-semibold text-accanto-900">
        Notifiche email
      </Text>
      <Text className="text-sm text-accanto-700">
        Scegli quali email vuoi ricevere. Le email di sicurezza (es. cambio
        password) vengono inviate sempre.
      </Text>

      {loading ? (
        <View className="py-2">
          <ActivityIndicator color="#334155" />
        </View>
      ) : (
        <View className="gap-2">
          {(prefs ?? []).map((p) => (
            <Pressable
              key={p.topic}
              onPress={() => toggle(p.topic)}
              accessibilityRole="switch"
              accessibilityState={{ checked: p.emailEnabled }}
              className="flex-row items-center justify-between gap-3 border border-accanto-100 rounded-lg px-3 py-3"
            >
              <Text className="text-sm text-accanto-800 flex-1">
                {TOPIC_LABEL[p.topic]}
              </Text>
              <View
                className={`w-12 h-7 rounded-full justify-center px-1 ${
                  p.emailEnabled ? 'bg-accanto-700' : 'bg-accanto-100'
                }`}
              >
                <View
                  className={`w-5 h-5 rounded-full bg-white ${
                    p.emailEnabled ? 'self-end' : 'self-start'
                  }`}
                />
              </View>
            </Pressable>
          ))}
        </View>
      )}

      <ErrorBanner message={error} />
      {msg ? <Text className="text-sm text-green-700">{msg}</Text> : null}
      <Button onPress={save} busy={saving} disabled={saving || !prefs}>
        {saving ? 'Salvataggio\u2026' : 'Salva preferenze'}
      </Button>
    </View>
  );
}
