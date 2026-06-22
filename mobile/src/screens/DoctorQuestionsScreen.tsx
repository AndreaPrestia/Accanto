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
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import TextField from '../components/ui/TextField';
import SelectField from '../components/ui/SelectField';
import ErrorBanner from '../components/ui/ErrorBanner';
import AiAssistPanel from '../components/AiAssistPanel';
import { api, extractError } from '../api/client';
import { doctorQuestionDraft } from '../api/ai';
import { useAiContext } from '../hooks/useAiContext';
import { useCircleId } from '../navigation/CircleContext';
import type {
  DoctorQuestion,
  DoctorQuestionCategory,
  DoctorQuestionStatus,
  DoctorQuestionTemplate
} from '@accanto/shared/types';
import {
  QuestionCategoryLabel,
  QuestionStatusLabel
} from '@accanto/shared/types';

const CATS: DoctorQuestionCategory[] = [
  'Diagnosis',
  'Therapy',
  'Pain',
  'Nutrition',
  'Hydration',
  'PalliativeCare',
  'Discharge',
  'HomeCare',
  'Emergency',
  'Prognosis',
  'Practical',
  'Other'
];

const STATUSES: DoctorQuestionStatus[] = [
  'ToAsk',
  'Asked',
  'Answered',
  'Archived'
];

if (
  Platform.OS === 'android' &&
  UIManager.setLayoutAnimationEnabledExperimental
) {
  UIManager.setLayoutAnimationEnabledExperimental(true);
}

type Prefill = { question: string; category: DoctorQuestionCategory };

export default function DoctorQuestionsScreen() {
  const circleId = useCircleId();
  const [items, setItems] = useState<DoctorQuestion[] | null>(null);
  const [templates, setTemplates] = useState<DoctorQuestionTemplate[]>([]);
  const [showForm, setShowForm] = useState(false);
  const [showTemplates, setShowTemplates] = useState(false);
  const [prefill, setPrefill] = useState<Prefill | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const { data } = await api.get<DoctorQuestion[]>(
        `/care-circles/${circleId}/doctor-questions`
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
      .get<DoctorQuestionTemplate[]>('/doctor-question-templates')
      .then((r) => setTemplates(r.data))
      .catch(() => {
        // Template panel è opzionale.
      });
  }, []);

  const updateStatus = async (
    q: DoctorQuestion,
    status: DoctorQuestionStatus
  ) => {
    try {
      await api.put(`/care-circles/${circleId}/doctor-questions/${q.id}`, {
        question: q.question,
        category: q.category,
        status,
        answerNotes: q.answerNotes ?? null
      });
      load();
    } catch (e) {
      setError(extractError(e));
    }
  };

  const del = (q: DoctorQuestion) => {
    Alert.alert('Eliminare questa domanda?', undefined, [
      { text: 'Annulla', style: 'cancel' },
      {
        text: 'Elimina',
        style: 'destructive',
        onPress: async () => {
          try {
            await api.delete(
              `/care-circles/${circleId}/doctor-questions/${q.id}`
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
        Domande per il medico
      </Text>
      <Text className="text-accanto-500 mb-4">
        Annota le domande quando ti vengono in mente. Riprenderle prima
        della visita aiuta.
      </Text>

      <View className="mb-4">
        <Button
          onPress={() => {
            setPrefill(null);
            setShowForm((s) => !s);
          }}
        >
          {showForm ? 'Annulla' : '+ Nuova domanda'}
        </Button>
      </View>

      {showForm ? (
        <NewForm
          circleId={circleId}
          prefill={prefill}
          onCreated={() => {
            setShowForm(false);
            setPrefill(null);
            load();
          }}
        />
      ) : null}

      <View className="mb-4">
        <DoctorQuestionsAiSection circleId={circleId} />
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
              Suggerimenti per categoria
            </Text>
            <Text className="text-accanto-500">
              {showTemplates ? '−' : '+'}
            </Text>
          </Pressable>
          {showTemplates ? (
            <View className="px-4 pb-4 gap-3">
              {templates.map((t) => (
                <View key={t.category}>
                  <Text className="text-sm font-medium text-accanto-900 mb-1">
                    {t.categoryLabel}
                  </Text>
                  <View className="gap-1">
                    {t.questions.map((q) => (
                      <Pressable
                        key={q}
                        onPress={() => {
                          setPrefill({ question: q, category: t.category });
                          setShowForm(true);
                        }}
                      >
                        <Text className="text-sm text-accanto-700">
                          + {q}
                        </Text>
                      </Pressable>
                    ))}
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
        <Text className="text-accanto-500">Ancora nessuna domanda.</Text>
      ) : (
        <View className="gap-3">
          {items.map((q) => (
            <QuestionCard
              key={q.id}
              q={q}
              onStatus={(s) => updateStatus(q, s)}
              onDelete={() => del(q)}
            />
          ))}
        </View>
      )}
    </Screen>
  );
}

function QuestionCard({
  q,
  onStatus,
  onDelete
}: {
  q: DoctorQuestion;
  onStatus: (s: DoctorQuestionStatus) => void;
  onDelete: () => void;
}) {
  const statusOptions = useMemo(
    () => STATUSES.map((s) => ({ value: s, label: QuestionStatusLabel[s] })),
    []
  );
  return (
    <View className="rounded-lg border border-accanto-100 bg-white p-4">
      <Text className="font-medium text-accanto-900">{q.question}</Text>
      <Text className="text-xs text-accanto-500 mt-1">
        {QuestionCategoryLabel[q.category]} •{' '}
        {QuestionStatusLabel[q.status]}
      </Text>
      {q.answerNotes ? (
        <Text className="text-sm text-accanto-900 mt-2">
          <Text className="text-accanto-500">Risposta: </Text>
          {q.answerNotes}
        </Text>
      ) : null}
      <View className="mt-3 gap-2">
        <SelectField
          label="Stato"
          value={q.status}
          onChange={(v) => v && onStatus(v as DoctorQuestionStatus)}
          options={statusOptions}
        />
        <Pressable onPress={onDelete} className="py-2">
          <Text className="text-sm text-accanto-500">Elimina</Text>
        </Pressable>
      </View>
    </View>
  );
}

function NewForm({
  circleId,
  prefill,
  onCreated
}: {
  circleId: string;
  prefill: Prefill | null;
  onCreated: () => void;
}) {
  const [question, setQuestion] = useState(prefill?.question ?? '');
  const [category, setCategory] = useState<DoctorQuestionCategory>(
    prefill?.category ?? 'Other'
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const catOptions = useMemo(
    () => CATS.map((c) => ({ value: c, label: QuestionCategoryLabel[c] })),
    []
  );

  const submit = async () => {
    if (!question.trim()) {
      setError('Scrivi la domanda.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await api.post(`/care-circles/${circleId}/doctor-questions`, {
        question: question.trim(),
        category
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
      <TextField
        label="Domanda"
        value={question}
        onChangeText={setQuestion}
        multiline
        numberOfLines={4}
        style={{ minHeight: 80, textAlignVertical: 'top' }}
      />
      <SelectField
        label="Categoria"
        value={category}
        onChange={(v) => v && setCategory(v as DoctorQuestionCategory)}
        options={catOptions}
      />
      <ErrorBanner message={error} />
      <Button onPress={submit} busy={busy} disabled={busy}>
        {busy ? 'Salvataggio…' : 'Aggiungi domanda'}
      </Button>
    </View>
  );
}

function DoctorQuestionsAiSection({ circleId }: { circleId: string }) {
  const { t } = useTranslation();
  const [topic, setTopic] = useState('');
  const [notes, setNotes] = useState('');
  const { systemAvailable, enabledForCircle, loading } = useAiContext(circleId);

  if (loading) return null;
  const disabled = !systemAvailable || !enabledForCircle;
  const disabledReason = !systemAvailable
    ? (t('ai.disabledSystem') as string)
    : (t('ai.disabledCircle') as string);

  return (
    <AiAssistPanel
      title={t('ai.doctorQuestionDraft.title') as string}
      description={t('ai.doctorQuestionDraft.description') as string}
      ctaLabel={t('ai.doctorQuestionDraft.cta') as string}
      disabled={disabled}
      disabledReason={disabledReason}
      onGenerate={() =>
        doctorQuestionDraft(circleId, topic.trim(), notes.trim() || undefined)
      }
    >
      <TextField
        label={t('ai.doctorQuestionDraft.topicLabel') as string}
        placeholder={t('ai.doctorQuestionDraft.topicPlaceholder') as string}
        value={topic}
        onChangeText={setTopic}
      />
      <TextField
        label={t('ai.doctorQuestionDraft.notesLabel') as string}
        value={notes}
        onChangeText={setNotes}
        multiline
        numberOfLines={2}
        style={{ minHeight: 60, textAlignVertical: 'top' }}
      />
    </AiAssistPanel>
  );
}
