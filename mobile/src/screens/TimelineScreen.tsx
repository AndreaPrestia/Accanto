import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Pressable,
  Text,
  View
} from 'react-native';
import { useFocusEffect, useNavigation, useRoute } from '@react-navigation/native';
import type { RouteProp } from '@react-navigation/native';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import TextField from '../components/ui/TextField';
import SelectField from '../components/ui/SelectField';
import DateField from '../components/ui/DateField';
import ErrorBanner from '../components/ui/ErrorBanner';
import AiAssistPanel from '../components/AiAssistPanel';
import { api, extractError } from '../api/client';
import { timelineSummary } from '../api/ai';
import { useAiContext } from '../hooks/useAiContext';
import { useCircleId } from '../navigation/CircleContext';
import type {
  TimelineEntry,
  TimelineEntryType,
  TimelineVisibility
} from '@accanto/shared/types';
import type { CircleTabParamList } from '../navigation/types';

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
  const navigation = useNavigation();
  const route = useRoute<RouteProp<CircleTabParamList, 'Timeline'>>();
  const { t } = useTranslation();

  const [entries, setEntries] = useState<TimelineEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [filterType, setFilterType] = useState<TimelineEntryType | ''>('');
  const [filterTag, setFilterTag] = useState('');
  const [filterFrom, setFilterFrom] = useState('');
  const [filterTo, setFilterTo] = useState('');
  const [showForm, setShowForm] = useState(false);

  // Bulk select
  const [selectMode, setSelectMode] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  // Deep-nav "quick action" dalla Dashboard: se il param openNew è true,
  // apri subito il form "Nuova voce" e resetta il param per evitare che si
  // riapra ad ogni tab switch/refocus.
  useEffect(() => {
    if (route.params?.openNew) {
      setShowForm(true);
      navigation.setParams({ openNew: undefined } as never);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [route.params?.openNew]);

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
    () => TYPES.map((type) => ({ value: type, label: t(`timelineType.${type}`) })),
    [t]
  );

  return (
    <Screen>
      <Text className="text-2xl font-semibold text-accanto-900 mb-1">{t('timeline.title')}</Text>
      <Text className="text-accanto-500 mb-4">
        {t('timeline.intro')}
      </Text>

      {/* Filtri */}
      <View className="flex-row gap-2">
        <View className="flex-1">
          <SelectField
            value={filterType}
            onChange={(v) => setFilterType(v as TimelineEntryType | '')}
            options={typeOptions}
            emptyLabel={t('timeline.allTypes') as string}
            placeholder={t('timeline.typePlaceholder') as string}
          />
        </View>
        <View className="flex-1">
          <TextField
            placeholder={t('timeline.filterTag') as string}
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
            label={t('timeline.from') as string}
            value={filterFrom}
            onChange={setFilterFrom}
            maximumDate={filterTo || undefined}
            clearable
          />
        </View>
        <View className="flex-1">
          <DateField
            label={t('timeline.to') as string}
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
            {t('timeline.clearFilters')}
          </Text>
        </Pressable>
      ) : null}

      <View className="mt-2 mb-4">
        <Button onPress={() => setShowForm((s) => !s)}>
          {showForm ? t('common.cancel') : t('timeline.addCta')}
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

      <View className="mb-4">
        <TimelineAiSection circleId={circleId} />
      </View>

      <BulkSelectBar
        selectMode={selectMode}
        selectedCount={selected.size}
        totalCount={entries?.length ?? 0}
        onToggle={() => {
          setSelectMode((m) => !m);
          setSelected(new Set());
        }}
        onSelectAll={() =>
          setSelected(new Set(entries?.map((e) => e.id) ?? []))
        }
      />

      {selectMode ? (
        <BulkEditForm
          circleId={circleId}
          selectedIds={selected}
          onApplied={() => {
            setSelected(new Set());
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
        <Text className="text-accanto-500">{t('timeline.empty')}</Text>
      ) : (
        <View className="gap-3">
          {entries.map((e) => (
            <EntryCard
              key={e.id}
              entry={e}
              circleId={circleId}
              onDeleted={load}
              selectMode={selectMode}
              selected={selected.has(e.id)}
              onToggleSelect={() =>
                setSelected((prev) => {
                  const next = new Set(prev);
                  if (next.has(e.id)) next.delete(e.id);
                  else next.add(e.id);
                  return next;
                })
              }
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
  onDeleted,
  selectMode,
  selected,
  onToggleSelect
}: {
  entry: TimelineEntry;
  circleId: string;
  onDeleted: () => void;
  selectMode: boolean;
  selected: boolean;
  onToggleSelect: () => void;
}) {
  const { t, i18n } = useTranslation();
  const [busy, setBusy] = useState(false);

  const del = () => {
    Alert.alert(t('timeline.deleteConfirmTitle'), t('timeline.deleteConfirmBody'), [
      { text: t('common.cancel'), style: 'cancel' },
      {
        text: t('common.delete'),
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

  const when = new Date(entry.occurredAt).toLocaleString(i18n.language);

  return (
    <Pressable
      onPress={selectMode ? onToggleSelect : undefined}
      className={`rounded-lg border bg-white p-4 ${
        selectMode && selected
          ? 'border-accanto-700 bg-accanto-50'
          : 'border-accanto-100'
      }`}
    >
      <View className="flex-row items-start justify-between gap-2">
        <View className="flex-row flex-1 items-start gap-2">
          {selectMode ? (
            <View
              className={`mt-0.5 h-5 w-5 rounded border items-center justify-center ${
                selected
                  ? 'bg-accanto-700 border-accanto-700'
                  : 'border-accanto-300 bg-white'
              }`}
            >
              {selected ? <Text className="text-white text-xs">✓</Text> : null}
            </View>
          ) : null}
          <View className="flex-1">
            <Text className="font-medium text-accanto-900">{entry.title}</Text>
            <Text className="text-xs text-accanto-500 mt-0.5">
              {when} • {t(`timelineType.${entry.type}`)} •{' '}
              {t(`visibility.${entry.visibility}`)}
            </Text>
          </View>
        </View>
        {selectMode ? null : (
          <Pressable
            onPress={del}
            disabled={busy}
            className="px-2 py-1"
          >
            <Text className="text-sm text-accanto-500">{t('common.delete')}</Text>
          </Pressable>
        )}
      </View>
      <Text className="mt-2 text-accanto-900">{entry.content}</Text>
      {entry.tags.length > 0 ? (
        <View className="mt-2 flex-row flex-wrap gap-1">
          {entry.tags.map((tag) => (
            <View
              key={tag}
              className="bg-accanto-100 rounded px-2 py-0.5"
            >
              <Text className="text-xs text-accanto-700">{tag}</Text>
            </View>
          ))}
        </View>
      ) : null}
    </Pressable>
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
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const typeOptions = useMemo(
    () => TYPES.map((type) => ({ value: type, label: t(`timelineType.${type}`) })),
    [t]
  );
  const visOptions = useMemo(
    () => VIS.map((v) => ({ value: v, label: t(`visibility.${v}`) })),
    [t]
  );

  const submit = async () => {
    if (!title.trim() || !content.trim() || !occurredAt) {
      setError(t('timeline.requiredFields'));
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
            label={t('timeline.whenLabel') as string}
            value={occurredAt}
            onChange={setOccurredAt}
            mode="datetime"
          />
        </View>
        <View className="flex-1">
          <SelectField
            label={t('timeline.typeLabel') as string}
            value={type}
            onChange={(v) => v && setType(v as TimelineEntryType)}
            options={typeOptions}
          />
        </View>
      </View>
      <TextField
        label={t('timeline.titleLabel') as string}
        value={title}
        onChangeText={setTitle}
      />
      <TextField
        label={t('timeline.detailLabel') as string}
        value={content}
        onChangeText={setContent}
        multiline
        numberOfLines={4}
        style={{ minHeight: 100, textAlignVertical: 'top' }}
      />
      <View className="flex-row gap-2">
        <View className="flex-1">
          <TextField
            label={t('timeline.tagsLabel') as string}
            value={tags}
            onChangeText={setTags}
            placeholder={t('timeline.tagsPlaceholder') as string}
            autoCapitalize="none"
            autoCorrect={false}
          />
        </View>
        <View className="flex-1">
          <SelectField
            label={t('timeline.visibilityLabel') as string}
            value={visibility}
            onChange={(v) => v && setVisibility(v as TimelineVisibility)}
            options={visOptions}
          />
        </View>
      </View>
      <ErrorBanner message={error} />
      <Button onPress={submit} busy={busy} disabled={busy}>
        {busy ? t('timeline.saving') : t('timeline.save')}
      </Button>
    </View>
  );
}

function BulkSelectBar({
  selectMode,
  selectedCount,
  totalCount,
  onToggle,
  onSelectAll
}: {
  selectMode: boolean;
  selectedCount: number;
  totalCount: number;
  onToggle: () => void;
  onSelectAll: () => void;
}) {
  const { t } = useTranslation();
  return (
    <View className="flex-row items-center gap-4 mb-3">
      <Pressable onPress={onToggle} className="py-1">
        <Text className="text-sm text-accanto-700 underline">
          {selectMode
            ? t('timeline.bulk.exit')
            : selectedCount > 0
            ? t('timeline.bulk.enterCount', { count: selectedCount })
            : t('timeline.bulk.enter')}
        </Text>
      </Pressable>
      {selectMode && totalCount > 0 ? (
        <Pressable onPress={onSelectAll} className="py-1">
          <Text className="text-sm text-accanto-500 underline">
            {t('timeline.bulk.selectAll', { count: totalCount })}
          </Text>
        </Pressable>
      ) : null}
    </View>
  );
}

function BulkEditForm({
  circleId,
  selectedIds,
  onApplied
}: {
  circleId: string;
  selectedIds: Set<string>;
  onApplied: () => void;
}) {
  const [bulkAddTags, setBulkAddTags] = useState('');
  const [bulkRemoveTags, setBulkRemoveTags] = useState('');
  const [bulkVisibility, setBulkVisibility] = useState<'' | TimelineVisibility>(
    ''
  );
  const [bulkBusy, setBulkBusy] = useState(false);
  const [bulkMsg, setBulkMsg] = useState<string | null>(null);
  const { t } = useTranslation();

  const visOptions = useMemo(
    () => VIS.map((v) => ({ value: v, label: t(`visibility.${v}`) })),
    [t]
  );

  const apply = async () => {
    const addTags = bulkAddTags
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean);
    const removeTags = bulkRemoveTags
      .split(',')
      .map((s) => s.trim())
      .filter(Boolean);
    if (addTags.length === 0 && removeTags.length === 0 && !bulkVisibility) {
      setBulkMsg(t('timeline.bulk.specifyChange'));
      return;
    }
    if (selectedIds.size === 0) return;
    Alert.alert(
      t('timeline.bulk.confirmTitle'),
      t('timeline.bulk.confirmBody', { count: selectedIds.size }),
      [
        { text: t('common.cancel'), style: 'cancel' },
        {
          text: t('timeline.bulk.apply'),
          onPress: async () => {
            setBulkBusy(true);
            setBulkMsg(null);
            try {
              const { data } = await api.patch<{
                updated: number;
                skipped: number;
              }>(`/care-circles/${circleId}/timeline/bulk`, {
                entryIds: Array.from(selectedIds),
                tagsToAdd: addTags.length ? addTags : null,
                tagsToRemove: removeTags.length ? removeTags : null,
                newVisibility: bulkVisibility || null
              });
              setBulkMsg(
                t('timeline.bulk.result', {
                  updated: data.updated,
                  skipped: data.skipped
                })
              );
              setBulkAddTags('');
              setBulkRemoveTags('');
              setBulkVisibility('');
              onApplied();
            } catch (e) {
              setBulkMsg(extractError(e));
            } finally {
              setBulkBusy(false);
            }
          }
        }
      ]
    );
  };

  return (
    <View className="rounded-lg border border-accanto-100 bg-white p-4 mb-4 gap-3">
      <Text className="font-medium text-accanto-900">
        {t('timeline.bulk.title', { count: selectedIds.size })}
      </Text>
      <View className="flex-row gap-2">
        <View className="flex-1">
          <TextField
            label={t('timeline.bulk.addTags') as string}
            value={bulkAddTags}
            onChangeText={setBulkAddTags}
            placeholder={t('timeline.bulk.addTagsPlaceholder') as string}
            autoCapitalize="none"
            autoCorrect={false}
          />
        </View>
        <View className="flex-1">
          <TextField
            label={t('timeline.bulk.removeTags') as string}
            value={bulkRemoveTags}
            onChangeText={setBulkRemoveTags}
            placeholder={t('timeline.bulk.removeTagsPlaceholder') as string}
            autoCapitalize="none"
            autoCorrect={false}
          />
        </View>
      </View>
      <SelectField
        label={t('timeline.visibilityLabel') as string}
        value={bulkVisibility}
        onChange={(v) => setBulkVisibility(v as '' | TimelineVisibility)}
        options={visOptions}
        emptyLabel={t('timeline.bulk.noChange') as string}
      />
      {bulkMsg ? (
        <Text className="text-sm text-accanto-700">{bulkMsg}</Text>
      ) : null}
      <Button
        onPress={apply}
        busy={bulkBusy}
        disabled={bulkBusy || selectedIds.size === 0}
      >
        {bulkBusy ? t('timeline.bulk.applying') : t('timeline.bulk.applyChanges')}
      </Button>
    </View>
  );
}

function TimelineAiSection({ circleId }: { circleId: string }) {
  const { t } = useTranslation();
  const [days, setDays] = useState('7');
  const { systemAvailable, enabledForCircle, loading } = useAiContext(circleId);

  if (loading) return null;
  const disabled = !systemAvailable || !enabledForCircle;
  const disabledReason = !systemAvailable
    ? (t('ai.disabledSystem') as string)
    : (t('ai.disabledCircle') as string);

  return (
    <AiAssistPanel
      title={t('ai.timelineSummary.title') as string}
      description={t('ai.timelineSummary.description') as string}
      ctaLabel={t('ai.timelineSummary.cta') as string}
      disabled={disabled}
      disabledReason={disabledReason}
      onGenerate={() =>
        timelineSummary(
          circleId,
          Math.max(1, Math.min(60, Number(days) || 7))
        )
      }
    >
      <TextField
        label={t('ai.timelineSummary.daysLabel') as string}
        value={days}
        onChangeText={setDays}
        keyboardType="number-pad"
      />
    </AiAssistPanel>
  );
}
