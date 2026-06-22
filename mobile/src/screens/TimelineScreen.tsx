import { useCallback, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Pressable,
  Text,
  View
} from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import TextField from '../components/ui/TextField';
import SelectField from '../components/ui/SelectField';
import DateField from '../components/ui/DateField';
import ErrorBanner from '../components/ui/ErrorBanner';
import { api, extractError } from '../api/client';
import { useCircleId } from '../navigation/CircleContext';
import type {
  TimelineEntry,
  TimelineEntryType,
  TimelineVisibility
} from '@accanto/shared/types';
import { TimelineTypeLabel, VisibilityLabel } from '@accanto/shared/types';

const TYPES: TimelineEntryType[] = [
  'MedicalUpdate',
  'Symptom',
  'Medication',
  'Appointment',
  'Decision',
  'PersonalNote',
  'Practical',
  'Other'
];
const VIS: TimelineVisibility[] = ['Circle', 'Private'];

export default function TimelineScreen() {
  const circleId = useCircleId();

  const [entries, setEntries] = useState<TimelineEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [filterType, setFilterType] = useState<TimelineEntryType | ''>('');
  const [filterTag, setFilterTag] = useState('');
  const [filterFrom, setFilterFrom] = useState('');
  const [filterTo, setFilterTo] = useState('');
  const [showForm, setShowForm] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      const params: Record<string, string> = {};
      if (filterType) params.type = filterType;
      if (filterTag.trim()) params.tag = filterTag.trim();
      if (filterFrom) {
        // Inizio giornata locale
        const d = new Date(filterFrom);
        d.setHours(0, 0, 0, 0);
        params.from = d.toISOString();
      }
      if (filterTo) {
        const d = new Date(filterTo);
        d.setHours(23, 59, 59, 999);
        params.to = d.toISOString();
      }
      const { data } = await api.get<TimelineEntry[]>(
        `/care-circles/${circleId}/timeline`,
        { params }
      );
      setEntries(data);
    } catch (e) {
      setError(extractError(e));
      setEntries([]);
    }
  }, [circleId, filterType, filterTag, filterFrom, filterTo]);

  useFocusEffect(
    useCallback(() => {
      load();
    }, [load])
  );

  const hasFilters = !!(filterType || filterTag.trim() || filterFrom || filterTo);
  const clearFilters = () => {
    setFilterType('');
    setFilterTag('');
    setFilterFrom('');
    setFilterTo('');
  };

  const typeOptions = useMemo(
    () => TYPES.map((t) => ({ value: t, label: TimelineTypeLabel[t] })),
    []
  );

  return (
    <Screen>
      <Text className="text-2xl font-semibold text-accanto-900 mb-1">Diario</Text>
      <Text className="text-accanto-500 mb-4">
        Tieni traccia di ci\u00f2 che succede, giorno per giorno.
      </Text>

      {/* Filtri */}
      <View className="flex-row gap-2">
        <View className="flex-1">
          <SelectField
            value={filterType}
            onChange={(v) => setFilterType(v as TimelineEntryType | '')}
            options={typeOptions}
            emptyLabel="Tutti i tipi"
            placeholder="Tipo"
          />
        </View>
        <View className="flex-1">
          <TextField
            placeholder="Filtra per tag"
            value={filterTag}
            onChangeText={setFilterTag}
            autoCapitalize="none"
            autoCorrect={false}
          />
        </View>
      </View>
      <View className="flex-row gap-2 mt-2">
        <View className="flex-1">
          <DateField
            label="Dal"
            value={filterFrom}
            onChange={setFilterFrom}
            maximumDate={filterTo || undefined}
            clearable
          />
        </View>
        <View className="flex-1">
          <DateField
            label="Al"
            value={filterTo}
            onChange={setFilterTo}
            minimumDate={filterFrom || undefined}
            clearable
          />
        </View>
      </View>
      {hasFilters ? (
        <Pressable onPress={clearFilters} className="mb-2 py-1">
          <Text className="text-sm text-accanto-700 underline">
            Pulisci filtri
          </Text>
        </Pressable>
      ) : null}

      <View className="mt-2 mb-4">
        <Button onPress={() => setShowForm((s) => !s)}>
          {showForm ? 'Annulla' : '+ Nuova voce'}
        </Button>
      </View>

      {showForm ? (
        <NewEntryForm
          circleId={circleId}
          onCreated={() => {
            setShowForm(false);
            load();
          }}
        />
      ) : null}

      <View className="mb-3">
        <ErrorBanner message={error} />
      </View>

      {entries === null ? (
        <View className="py-6 items-center">
          <ActivityIndicator color="#334155" />
        </View>
      ) : entries.length === 0 ? (
        <Text className="text-accanto-500">Ancora nessuna voce.</Text>
      ) : (
        <View className="gap-3">
          {entries.map((e) => (
            <EntryCard
              key={e.id}
              entry={e}
              circleId={circleId}
              onDeleted={load}
            />
          ))}
        </View>
      )}
    </Screen>
  );
}

function EntryCard({
  entry,
  circleId,
  onDeleted
}: {
  entry: TimelineEntry;
  circleId: string;
  onDeleted: () => void;
}) {
  const [busy, setBusy] = useState(false);

  const del = () => {
    Alert.alert('Eliminare questa voce?', 'L\u2019azione non si pu\u00f2 annullare.', [
      { text: 'Annulla', style: 'cancel' },
      {
        text: 'Elimina',
        style: 'destructive',
        onPress: async () => {
          setBusy(true);
          try {
            await api.delete(
              `/care-circles/${circleId}/timeline/${entry.id}`
            );
            onDeleted();
          } finally {
            setBusy(false);
          }
        }
      }
    ]);
  };

  const when = new Date(entry.occurredAt).toLocaleString('it-IT');

  return (
    <View className="rounded-lg border border-accanto-100 bg-white p-4">
      <View className="flex-row items-start justify-between gap-2">
        <View className="flex-1">
          <Text className="font-medium text-accanto-900">{entry.title}</Text>
          <Text className="text-xs text-accanto-500 mt-0.5">
            {when} \u2022 {TimelineTypeLabel[entry.type]} \u2022{' '}
            {VisibilityLabel[entry.visibility]}
          </Text>
        </View>
        <Pressable
          onPress={del}
          disabled={busy}
          className="px-2 py-1"
        >
          <Text className="text-sm text-accanto-500">Elimina</Text>
        </Pressable>
      </View>
      <Text className="mt-2 text-accanto-900">{entry.content}</Text>
      {entry.tags.length > 0 ? (
        <View className="mt-2 flex-row flex-wrap gap-1">
          {entry.tags.map((t) => (
            <View
              key={t}
              className="bg-accanto-100 rounded px-2 py-0.5"
            >
              <Text className="text-xs text-accanto-700">{t}</Text>
            </View>
          ))}
        </View>
      ) : null}
    </View>
  );
}

function NewEntryForm({
  circleId,
  onCreated
}: {
  circleId: string;
  onCreated: () => void;
}) {
  const [occurredAt, setOccurredAt] = useState<string>(new Date().toISOString());
  const [type, setType] = useState<TimelineEntryType>('MedicalUpdate');
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [tags, setTags] = useState('');
  const [visibility, setVisibility] = useState<TimelineVisibility>('Circle');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const typeOptions = useMemo(
    () => TYPES.map((t) => ({ value: t, label: TimelineTypeLabel[t] })),
    []
  );
  const visOptions = useMemo(
    () => VIS.map((v) => ({ value: v, label: VisibilityLabel[v] })),
    []
  );

  const submit = async () => {
    if (!title.trim() || !content.trim() || !occurredAt) {
      setError('Compila tutti i campi obbligatori.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await api.post(`/care-circles/${circleId}/timeline`, {
        occurredAt,
        type,
        title: title.trim(),
        content: content.trim(),
        tags: tags
          .split(',')
          .map((s) => s.trim())
          .filter(Boolean),
        visibility
      });
      onCreated();
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <View className="rounded-lg border border-accanto-100 bg-white p-4 mb-4 gap-3">
      <View className="flex-row gap-2">
        <View className="flex-1">
          <DateField
            label="Quando"
            value={occurredAt}
            onChange={setOccurredAt}
            mode="datetime"
          />
        </View>
        <View className="flex-1">
          <SelectField
            label="Tipo"
            value={type}
            onChange={(v) => v && setType(v as TimelineEntryType)}
            options={typeOptions}
          />
        </View>
      </View>
      <TextField
        label="Titolo"
        value={title}
        onChangeText={setTitle}
      />
      <TextField
        label="Dettaglio"
        value={content}
        onChangeText={setContent}
        multiline
        numberOfLines={4}
        style={{ minHeight: 100, textAlignVertical: 'top' }}
      />
      <View className="flex-row gap-2">
        <View className="flex-1">
          <TextField
            label="Tag (separati da virgola)"
            value={tags}
            onChangeText={setTags}
            placeholder="Es. visita, farmaci"
            autoCapitalize="none"
            autoCorrect={false}
          />
        </View>
        <View className="flex-1">
          <SelectField
            label="Visibilit\u00e0"
            value={visibility}
            onChange={(v) => v && setVisibility(v as TimelineVisibility)}
            options={visOptions}
          />
        </View>
      </View>
      <ErrorBanner message={error} />
      <Button onPress={submit} busy={busy} disabled={busy}>
        {busy ? 'Salvataggio\u2026' : 'Salva voce'}
      </Button>
    </View>
  );
}
