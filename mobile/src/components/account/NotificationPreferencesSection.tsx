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

interface Pref {
  topic: Topic;
  emailEnabled: boolean;
  /**
   * Nullable lato DTO server (retro-compat con client web), ma il GET
   * lo restituisce sempre valorizzato. Trattato come bool con
   * fallback a true se null/undefined.
   */
  pushEnabled: boolean | null;
}

const TOPIC_LABEL: Record<Topic, string> = {
  TimelineEntryCreated: 'Nuove voci nel diario',
  SharedUpdateCreated: 'Nuovi aggiornamenti condivisi',
  DoctorQuestionAnswered: 'Risposte alle domande al medico',
  InviteAccepted: 'Nuove persone entrate in un cerchio'
};

type Channel = 'email' | 'push';

function Switch({
  on,
  label,
  onPress
}: {
  on: boolean;
  label: string;
  onPress: () => void;
}) {
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="switch"
      accessibilityLabel={label}
      accessibilityState={{ checked: on }}
      className="flex-row items-center gap-2"
    >
      <View
        className={`w-12 h-7 rounded-full justify-center px-1 ${on ? 'bg-accanto-700' : 'bg-accanto-100'}`}
      >
        <View
          className={`w-5 h-5 rounded-full bg-white ${on ? 'self-end' : 'self-start'}`}
        />
      </View>
      <Text className="text-xs text-accanto-500">{label}</Text>
    </Pressable>
  );
}

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

  const toggle = (topic: Topic, channel: Channel) => {
    setMsg(null);
    setPrefs((cur) =>
      (cur ?? []).map((p) => {
        if (p.topic !== topic) return p;
        if (channel === 'email') return { ...p, emailEnabled: !p.emailEnabled };
        const current = p.pushEnabled ?? true;
        return { ...p, pushEnabled: !current };
      })
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
        Preferenze notifiche
      </Text>
      <Text className="text-sm text-accanto-700">
        Scegli quali notifiche ricevere via email e via push. Le email di
        sicurezza (es. cambio password) vengono inviate sempre.
      </Text>

      {loading ? (
        <View className="py-2">
          <ActivityIndicator color="#334155" />
        </View>
      ) : (
        <View className="gap-2">
          {(prefs ?? []).map((p) => {
            const pushOn = p.pushEnabled ?? true;
            return (
              <View
                key={p.topic}
                className="gap-2 border border-accanto-100 rounded-lg px-3 py-3"
              >
                <Text className="text-sm text-accanto-800">
                  {TOPIC_LABEL[p.topic]}
                </Text>
                <View className="flex-row gap-4 flex-wrap">
                  <Switch
                    on={p.emailEnabled}
                    label="Email"
                    onPress={() => toggle(p.topic, 'email')}
                  />
                  <Switch
                    on={pushOn}
                    label="Push"
                    onPress={() => toggle(p.topic, 'push')}
                  />
                </View>
              </View>
            );
          })}
        </View>
      )}

      <ErrorBanner message={error} />
      {msg ? <Text className="text-sm text-green-700">{msg}</Text> : null}
      <Button onPress={save} busy={saving} disabled={saving || !prefs}>
        {saving ? 'Salvataggio…' : 'Salva preferenze'}
      </Button>
    </View>
  );
}
