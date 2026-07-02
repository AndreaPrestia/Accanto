import { useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Modal,
  Pressable,
  ScrollView,
  Text,
  View
} from 'react-native';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import ErrorBanner from '../components/ui/ErrorBanner';
import SelectField from '../components/ui/SelectField';
import { useOptionalCircleId } from '../navigation/CircleContext';
import {
  listAiInteractions,
  getAiInteraction,
  type AiInteractionDetail,
  type AiInteractionSummary
} from '../api/ai';
import { api, extractError } from '../api/client';
import type { CareCircle } from '@accanto/shared/types';

const PAGE_SIZE = 20;

/**
 * Storico interazioni AI: lista paginata + modale di dettaglio con input/output
 * grezzi. Quando lo screen è montato sotto CircleStack legge il circleId dal
 * contesto e filtra automaticamente; dal drawer globale mostra tutte le
 * interazioni dell'utente e propone un dropdown per filtrare su un cerchio
 * specifico (unifica il vecchio link "AI del cerchio").
 */
export default function AiHistoryScreen() {
  const contextCircleId = useOptionalCircleId() ?? undefined;
  const { t, i18n } = useTranslation();

  // Se veniamo montati sotto CircleStack (`AiHistoryCircle`), il context fissa
  // il filtro; altrimenti l'utente sceglie da dropdown. `selectedCircleId` ha
  // '' quando si vogliono TUTTE le interazioni.
  const [selectedCircleId, setSelectedCircleId] = useState<string>(
    contextCircleId ?? ''
  );
  const activeCircleId = contextCircleId ?? selectedCircleId ?? '';

  const [myCircles, setMyCircles] = useState<CareCircle[]>([]);
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<AiInteractionSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<AiInteractionDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  // Cerchi dell'utente per popolare il dropdown filtro. Se contextCircleId
  // è già impostato dal CircleStack non serve caricare la lista (il filtro
  // è forzato).
  useEffect(() => {
    if (contextCircleId) return;
    let cancelled = false;
    api
      .get<CareCircle[]>('/care-circles')
      .then((r) => {
        if (!cancelled) setMyCircles(r.data);
      })
      .catch(() => {
        /* silenzioso: dropdown mostra solo "Tutti" */
      });
    return () => {
      cancelled = true;
    };
  }, [contextCircleId]);

  const circleOptions = useMemo(
    () => myCircles.map((c) => ({ value: c.id, label: c.name })),
    [myCircles]
  );

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    listAiInteractions({
      circleId: activeCircleId || undefined,
      page,
      pageSize: PAGE_SIZE
    })
      .then((r) => {
        if (cancelled) return;
        setItems(r.items);
        setTotal(r.total);
      })
      .catch((e) => {
        if (!cancelled) setError(extractError(e));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [activeCircleId, page]);

  // Cambiando cerchio nel dropdown ripartiamo dalla pagina 1.
  const onSelectCircle = (v: string) => {
    setSelectedCircleId(v);
    setPage(1);
  };

  const open = async (id: string) => {
    setDetailLoading(true);
    try {
      const d = await getAiInteraction(id);
      setSelected(d);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setDetailLoading(false);
    }
  };

  const fmtDate = (s: string) => {
    try {
      return new Date(s).toLocaleString(i18n.language);
    } catch {
      return s;
    }
  };

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <Screen>
      <Text className="text-2xl font-semibold text-accanto-900 mb-1">
        {t('ai.history.title')}
      </Text>
      <Text className="text-accanto-500 mb-4">
        {t('ai.history.subtitle')}
      </Text>

      {/* Dropdown filtro cerchio: nascosto se lo screen è montato sotto
          CircleStack (contextCircleId presente), il filtro è già forzato. */}
      {!contextCircleId ? (
        <View className="mb-3">
          <SelectField
            label={t('ai.history.circleFilter.label')}
            value={selectedCircleId}
            onChange={(v) => onSelectCircle(v as string)}
            options={circleOptions}
            emptyLabel={t('ai.history.circleFilter.all')}
          />
        </View>
      ) : null}

      {activeCircleId ? (
        <Text className="text-xs text-accanto-500 mb-3">
          {t('ai.history.circleSectionHint') as string}
        </Text>
      ) : (
        <Text className="text-xs text-accanto-500 mb-3">
          {t('ai.history.circleFilter.hint') as string}
        </Text>
      )}

      <ErrorBanner message={error} />

      {loading && items.length === 0 ? (
        <View className="py-6 items-center">
          <ActivityIndicator color="#334155" />
        </View>
      ) : items.length === 0 ? (
        <Text className="text-accanto-500">{t('ai.history.empty')}</Text>
      ) : (
        <View className="gap-2">
          {items.map((it) => (
            <Pressable
              key={it.id}
              onPress={() => open(it.id)}
              className="rounded-lg border border-accanto-100 bg-white p-3"
            >
              <View className="flex-row items-baseline justify-between gap-2">
                <Text className="font-medium text-accanto-900 flex-1">
                  {it.function}
                </Text>
                <Text className="text-xs text-accanto-500">
                  {fmtDate(it.createdAt)}
                </Text>
              </View>
              <Text className="text-xs text-accanto-500 mt-1">
                {t('ai.history.verdict')}: {it.verdict} ·{' '}
                {t('ai.history.feedback')}: {it.feedback ?? '—'} ·{' '}
                {it.model}
              </Text>
            </Pressable>
          ))}
        </View>
      )}

      {totalPages > 1 ? (
        <View className="mt-4 flex-row items-center gap-3">
          <Pressable
            onPress={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page <= 1}
            className={page <= 1 ? 'opacity-50' : ''}
          >
            <Text className="text-accanto-700 text-lg">←</Text>
          </Pressable>
          <Text className="text-sm text-accanto-700">
            {page} / {totalPages}
          </Text>
          <Pressable
            onPress={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={page >= totalPages}
            className={page >= totalPages ? 'opacity-50' : ''}
          >
            <Text className="text-accanto-700 text-lg">→</Text>
          </Pressable>
        </View>
      ) : null}

      <Modal
        visible={!!selected}
        transparent
        animationType="slide"
        onRequestClose={() => setSelected(null)}
      >
        <View className="flex-1 bg-black/40 justify-end">
          <View className="bg-white rounded-t-2xl max-h-[85%]">
            {selected ? (
              <>
                <View className="flex-row items-center justify-between px-4 py-3 border-b border-accanto-100">
                  <Text className="font-medium text-accanto-900 flex-1">
                    {selected.function}
                  </Text>
                  <Pressable onPress={() => setSelected(null)} className="p-2">
                    <Text className="text-accanto-500 text-lg">×</Text>
                  </Pressable>
                </View>
                <ScrollView className="px-4 py-3">
                  <Text className="text-xs text-accanto-500 mb-3">
                    {fmtDate(selected.createdAt)} · {selected.model} ·{' '}
                    {selected.verdict}
                    {selected.cacheHit ? ' · cache' : ''}
                  </Text>
                  <Text className="text-sm font-medium text-accanto-900 mb-1">
                    {t('ai.history.input') as string}
                  </Text>
                  <View className="rounded border border-accanto-100 bg-accanto-50 p-2 mb-3">
                    <Text className="text-xs text-accanto-900">
                      {selected.input}
                    </Text>
                  </View>
                  <Text className="text-sm font-medium text-accanto-900 mb-1">
                    {t('ai.history.output') as string}
                  </Text>
                  <View className="rounded border border-accanto-100 bg-accanto-50 p-2 mb-6">
                    <Text className="text-xs text-accanto-900">
                      {selected.output}
                    </Text>
                  </View>
                </ScrollView>
              </>
            ) : null}
          </View>
        </View>
      </Modal>

      {detailLoading ? (
        <View className="absolute bottom-4 right-4">
          <ActivityIndicator color="#334155" />
        </View>
      ) : null}
    </Screen>
  );
}
