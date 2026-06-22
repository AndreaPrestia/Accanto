import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  LayoutAnimation,
  Platform,
  Pressable,
  Text,
  UIManager,
  View
} from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import * as Clipboard from 'expo-clipboard';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import TextField from '../components/ui/TextField';
import SelectField from '../components/ui/SelectField';
import ErrorBanner from '../components/ui/ErrorBanner';
import { api, extractError } from '../api/client';
import { useCircleId } from '../navigation/CircleContext';
import type {
  SharedUpdate,
  SharedUpdateAudience,
  SharedUpdateTemplate
} from '@accanto/shared/types';
import { AudienceLabel } from '@accanto/shared/types';

const AUDIENCES: SharedUpdateAudience[] = [
  'CloseFamily',
  'ExtendedFamily',
  'Friends',
  'Generic'
];

if (
  Platform.OS === 'android' &&
  UIManager.setLayoutAnimationEnabledExperimental
) {
  UIManager.setLayoutAnimationEnabledExperimental(true);
}

export default function SharedUpdatesScreen() {
  const circleId = useCircleId();
  const [items, setItems] = useState<SharedUpdate[] | null>(null);
  const [templates, setTemplates] = useState<SharedUpdateTemplate[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [showTemplates, setShowTemplates] = useState(false);
  const [prefill, setPrefill] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const [copiedId, setCopiedId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const { data } = await api.get<SharedUpdate[]>(
        `/care-circles/${circleId}/shared-updates`
      );
      setItems(data);
    } catch (e) {
      setError(extractError(e));
      setItems([]);
    }
  }, [circleId]);

  useFocusEffect(
    useCallback(() => {
      load();
    }, [load])
  );

  useEffect(() => {
    api
      .get<SharedUpdateTemplate[]>('/shared-update-templates')
      .then((r) => setTemplates(r.data))
      .catch(() => {
        // Template panel \u00e8 opzionale.
      });
  }, []);

  const copy = async (text: string, key: string) => {
    try {
      await Clipboard.setStringAsync(text);
      setCopiedId(key);
      setTimeout(() => setCopiedId(null), 2000);
    } catch {
      Alert.alert(
        'Impossibile copiare',
        'Selezionalo e copialo a mano.'
      );
    }
  };

  const del = (u: SharedUpdate) => {
    Alert.alert('Eliminare questo aggiornamento?', undefined, [
      { text: 'Annulla', style: 'cancel' },
      {
        text: 'Elimina',
        style: 'destructive',
        onPress: async () => {
          try {
            await api.delete(
              `/care-circles/${circleId}/shared-updates/${u.id}`
            );
            load();
          } catch (e) {
            setError(extractError(e));
          }
        }
      }
    ]);
  };

  return (
    <Screen>
      <Text className="text-2xl font-semibold text-accanto-900 mb-1">
        Aggiornamenti per gli altri
      </Text>
      <Text className="text-accanto-500 mb-4">
        Componi un messaggio una volta sola, poi copialo e invialo dove
        preferisci.
      </Text>

      <View className="mb-4">
        <Button
          onPress={() => {
            setPrefill('');
            setShowForm((s) => !s);
          }}
        >
          {showForm ? 'Annulla' : '+ Nuovo aggiornamento'}
        </Button>
      </View>

      {showForm ? (
        <NewForm
          circleId={circleId}
          prefill={prefill}
          onCreated={() => {
            setShowForm(false);
            setPrefill('');
            load();
          }}
        />
      ) : null}

      {templates.length > 0 ? (
        <View className="rounded-lg border border-accanto-100 bg-white mb-4 overflow-hidden">
          <Pressable
            onPress={() => {
              LayoutAnimation.easeInEaseOut();
              setShowTemplates((s) => !s);
            }}
            className="px-4 py-3 flex-row items-center justify-between"
          >
            <Text className="font-medium text-accanto-900">Modelli pronti</Text>
            <Text className="text-accanto-500">
              {showTemplates ? '\u2212' : '+'}
            </Text>
          </Pressable>
          {showTemplates ? (
            <View className="px-4 pb-4 gap-3">
              {templates.map((t) => (
                <View key={t.title}>
                  <Text className="text-sm font-medium text-accanto-900 mb-1">
                    {t.title}
                  </Text>
                  <Text className="text-sm text-accanto-700">
                    {t.content}
                  </Text>
                  <View className="mt-2">
                    <Button
                      variant="ghost"
                      onPress={() => {
                        setPrefill(t.content);
                        setShowForm(true);
                      }}
                    >
                      Usa come base
                    </Button>
                  </View>
                </View>
              ))}
            </View>
          ) : null}
        </View>
      ) : null}

      <View className="mb-3">
        <ErrorBanner message={error} />
      </View>

      {items === null ? (
        <View className="py-6 items-center">
          <ActivityIndicator color="#334155" />
        </View>
      ) : items.length === 0 ? (
        <Text className="text-accanto-500">
          Ancora nessun aggiornamento.
        </Text>
      ) : (
        <View className="gap-3">
          {items.map((u) => (
            <View
              key={u.id}
              className="rounded-lg border border-accanto-100 bg-white p-4"
            >
              <Text className="text-xs text-accanto-500">
                {AudienceLabel[u.audience]} \u2022{' '}
                {new Date(u.createdAt).toLocaleString('it-IT')}
              </Text>
              <Text className="mt-2 text-accanto-900">{u.content}</Text>
              <View className="mt-3 flex-row gap-2">
                <View className="flex-1">
                  <Button
                    variant="ghost"
                    onPress={() => copy(u.content, u.id)}
                  >
                    {copiedId === u.id ? 'Copiato!' : 'Copia testo'}
                  </Button>
                </View>
                <Pressable
                  onPress={() => del(u)}
                  className="px-3 py-2 items-center justify-center"
                >
                  <Text className="text-sm text-accanto-500">Elimina</Text>
                </Pressable>
              </View>
            </View>
          ))}
        </View>
      )}
    </Screen>
  );
}

function NewForm({
  circleId,
  prefill,
  onCreated
}: {
  circleId: string;
  prefill: string;
  onCreated: () => void;
}) {
  const [audience, setAudience] = useState<SharedUpdateAudience>('CloseFamily');
  const [content, setContent] = useState(prefill);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const audienceOptions = useMemo(
    () => AUDIENCES.map((a) => ({ value: a, label: AudienceLabel[a] })),
    []
  );

  const submit = async () => {
    if (!content.trim()) {
      setError('Scrivi il messaggio.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await api.post(`/care-circles/${circleId}/shared-updates`, {
        audience,
        content: content.trim()
      });
      onCreated();
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <View className="rounded-lg border border-accanto-100 bg-white p-4 mb-4 gap-3">
      <SelectField
        label="A chi \u00e8 rivolto"
        value={audience}
        onChange={(v) => v && setAudience(v as SharedUpdateAudience)}
        options={audienceOptions}
      />
      <TextField
        label="Messaggio"
        value={content}
        onChangeText={setContent}
        multiline
        numberOfLines={6}
        style={{ minHeight: 140, textAlignVertical: 'top' }}
      />
      <ErrorBanner message={error} />
      <Button onPress={submit} busy={busy} disabled={busy}>
        {busy ? 'Salvataggio\u2026' : 'Salva aggiornamento'}
      </Button>
    </View>
  );
}
