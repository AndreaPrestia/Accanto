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
import { useTranslation } from 'react-i18next';
import * as Clipboard from 'expo-clipboard';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import TextField from '../components/ui/TextField';
import SelectField from '../components/ui/SelectField';
import ErrorBanner from '../components/ui/ErrorBanner';
import AiAssistPanel from '../components/AiAssistPanel';
import { api, extractError } from '../api/client';
import { rephrase } from '../api/ai';
import { useAiContext } from '../hooks/useAiContext';
import { useCircleId } from '../navigation/CircleContext';
import type {
  SharedUpdate,
  SharedUpdateAudience,
  SharedUpdateTemplate
} from '@accanto/shared/types';

const AUDIENCES: SharedUpdateAudience[] = [
  'CloseFamily',
  'ExtendedFamily',
  'Friends',
  'Generic'
];

const AUDIENCE_I18N_KEY: Record<SharedUpdateAudience, string> = {
  CloseFamily: 'sharedUpdates.audience.closeFamily',
  ExtendedFamily: 'sharedUpdates.audience.extendedFamily',
  Friends: 'sharedUpdates.audience.friends',
  Generic: 'sharedUpdates.audience.generic'
};

if (
  Platform.OS === 'android' &&
  UIManager.setLayoutAnimationEnabledExperimental
) {
  UIManager.setLayoutAnimationEnabledExperimental(true);
}

export default function SharedUpdatesScreen() {
  const { t, i18n } = useTranslation();
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
        // Template panel è opzionale.
      });
  }, []);

  const copy = async (text: string, key: string) => {
    try {
      await Clipboard.setStringAsync(text);
      setCopiedId(key);
      setTimeout(() => setCopiedId(null), 2000);
    } catch {
      Alert.alert(
        t('sharedUpdates.copyErrorTitle'),
        t('sharedUpdates.copyErrorBody')
      );
    }
  };

  const del = (u: SharedUpdate) => {
    Alert.alert(t('sharedUpdates.deleteConfirm'), undefined, [
      { text: t('common.cancel'), style: 'cancel' },
      {
        text: t('common.delete'),
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
        {t('sharedUpdates.title')}
      </Text>
      <Text className="text-accanto-500 mb-1">
        {t('sharedUpdates.intro')}
      </Text>
      <Text className="text-accanto-500 mb-4">
        {t('sharedUpdates.introBalance')}
      </Text>

      <View className="mb-4">
        <Button
          onPress={() => {
            setPrefill('');
            setShowForm((s) => !s);
          }}
        >
          {showForm ? t('common.cancel') : t('sharedUpdates.newUpdate')}
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

      <View className="mb-4">
        <SharedUpdatesAiSection circleId={circleId} />
      </View>

      {templates.length > 0 ? (
        <View className="rounded-lg border border-accanto-100 bg-white mb-4 overflow-hidden">
          <Pressable
            onPress={() => {
              LayoutAnimation.easeInEaseOut();
              setShowTemplates((s) => !s);
            }}
            className="px-4 py-3 flex-row items-center justify-between"
          >
            <Text className="font-medium text-accanto-900">
              {t('sharedUpdates.templatesPanel')}
            </Text>
            <Text className="text-accanto-500">
              {showTemplates ? '−' : '+'}
            </Text>
          </Pressable>
          {showTemplates ? (
            <View className="px-4 pb-4 gap-3">
              {templates.map((tpl) => (
                <View key={tpl.title}>
                  <Text className="text-sm font-medium text-accanto-900 mb-1">
                    {tpl.title}
                  </Text>
                  <Text className="text-sm text-accanto-700">
                    {tpl.content}
                  </Text>
                  <View className="mt-2">
                    <Button
                      variant="ghost"
                      onPress={() => {
                        setPrefill(tpl.content);
                        setShowForm(true);
                      }}
                    >
                      {t('sharedUpdates.useAsBase')}
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
          {t('sharedUpdates.empty')}
        </Text>
      ) : (
        <View className="gap-3">
          {items.map((u) => (
            <View
              key={u.id}
              className="rounded-lg border border-accanto-100 bg-white p-4"
            >
              <Text className="text-xs text-accanto-500">
                {t(AUDIENCE_I18N_KEY[u.audience])} •{' '}
                {new Date(u.createdAt).toLocaleString(i18n.language)}
              </Text>
              <Text className="mt-2 text-accanto-900">{u.content}</Text>
              <View className="mt-3 flex-row gap-2">
                <View className="flex-1">
                  <Button
                    variant="ghost"
                    onPress={() => copy(u.content, u.id)}
                  >
                    {copiedId === u.id
                      ? t('sharedUpdates.copied')
                      : t('sharedUpdates.copyText')}
                  </Button>
                </View>
                <Pressable
                  onPress={() => del(u)}
                  className="px-3 py-2 items-center justify-center"
                >
                  <Text className="text-sm text-accanto-500">
                    {t('common.delete')}
                  </Text>
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
  const { t } = useTranslation();
  const [audience, setAudience] = useState<SharedUpdateAudience>('CloseFamily');
  const [content, setContent] = useState(prefill);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const audienceOptions = useMemo(
    () =>
      AUDIENCES.map((a) => ({
        value: a,
        label: t(AUDIENCE_I18N_KEY[a]) as string
      })),
    [t]
  );

  const submit = async () => {
    if (!content.trim()) {
      setError(t('sharedUpdates.writeMessageError'));
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
        label={t('sharedUpdates.audienceLabel') as string}
        value={audience}
        onChange={(v) => v && setAudience(v as SharedUpdateAudience)}
        options={audienceOptions}
      />
      <TextField
        label={t('sharedUpdates.messageLabel') as string}
        value={content}
        onChangeText={setContent}
        multiline
        numberOfLines={6}
        style={{ minHeight: 140, textAlignVertical: 'top' }}
      />
      <ErrorBanner message={error} />
      <Button onPress={submit} busy={busy} disabled={busy}>
        {busy ? t('common.saving') : t('sharedUpdates.save')}
      </Button>
    </View>
  );
}

function SharedUpdatesAiSection({ circleId }: { circleId: string }) {
  const { t } = useTranslation();
  const [text, setText] = useState('');
  const [tone, setTone] = useState<
    'neutral' | 'warm' | 'concise' | 'hopeful' | 'encouraging'
  >('warm');
  const { systemAvailable, enabledForCircle, loading } = useAiContext(circleId);

  const toneOptions = useMemo(
    () => [
      {
        value: 'neutral',
        label: t('ai.rephrase.toneOptions.neutral') as string
      },
      {
        value: 'warm',
        label: t('ai.rephrase.toneOptions.warm') as string
      },
      {
        value: 'concise',
        label: t('ai.rephrase.toneOptions.concise') as string
      },
      {
        value: 'hopeful',
        label: t('ai.rephrase.toneOptions.hopeful') as string
      },
      {
        value: 'encouraging',
        label: t('ai.rephrase.toneOptions.encouraging') as string
      }
    ],
    [t]
  );

  if (loading) return null;
  const disabled = !systemAvailable || !enabledForCircle;
  const disabledReason = !systemAvailable
    ? (t('ai.disabledSystem') as string)
    : (t('ai.disabledCircle') as string);

  return (
    <AiAssistPanel
      title={t('ai.rephrase.title') as string}
      description={t('ai.rephrase.description') as string}
      ctaLabel={t('ai.rephrase.cta') as string}
      disabled={disabled}
      disabledReason={disabledReason}
      onGenerate={() => rephrase(circleId, text.trim(), tone)}
    >
      <TextField
        label={t('ai.rephrase.textLabel') as string}
        value={text}
        onChangeText={setText}
        multiline
        numberOfLines={3}
        style={{ minHeight: 80, textAlignVertical: 'top' }}
      />
      <SelectField
        label={t('ai.rephrase.toneLabel') as string}
        value={tone}
        onChange={(v) =>
          v &&
          setTone(
            v as 'neutral' | 'warm' | 'concise' | 'hopeful' | 'encouraging'
          )
        }
        options={toneOptions}
      />
    </AiAssistPanel>
  );
}
