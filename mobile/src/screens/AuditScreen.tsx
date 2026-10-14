import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import ErrorBanner from '../components/ui/ErrorBanner';
import { api, extractError } from '../api/client';
import { useCircleId } from '../navigation/CircleContext';

type AuditEntry = {
  id: string;
  careCircleId: string;
  performedByUserId: string;
  performedByDisplayName: string | null;
  actionType: string;
  resourceType: string;
  resourceId: string | null;
  summary: string | null;
  timestamp: string;
};

type Page = {
  items: AuditEntry[];
  total: number;
  skip: number;
  take: number;
};

const PAGE_SIZE = 50;

export default function AuditScreen() {
  const circleId = useCircleId();
  const { t, i18n } = useTranslation();
  const [items, setItems] = useState<AuditEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [skip, setSkip] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    async (from: number, replace: boolean) => {
      setLoading(true);
      setError(null);
      try {
        const { data } = await api.get<Page>(
          `/care-circles/${circleId}/audit`,
          { params: { skip: from, take: PAGE_SIZE } }
        );
        setTotal(data.total);
        setSkip(from + data.items.length);
        setItems((prev) => (replace ? data.items : [...prev, ...data.items]));
      } catch (e) {
        setError(extractError(e));
      } finally {
        setLoading(false);
      }
    },
    [circleId]
  );

  useEffect(() => {
    load(0, true);
  }, [load]);

  return (
    <Screen>
      <Text className="text-2xl font-semibold text-accanto-900 mb-1">
        {t('audit.title')}
      </Text>
      <Text className="text-accanto-500 mb-4">
        {t('audit.intro')}
      </Text>

      <ErrorBanner message={error} />

      {items.length === 0 && !loading ? (
        <Text className="text-accanto-500">{t('audit.empty')}</Text>
      ) : (
        <View className="gap-2">
          {items.map((e) => (
            <View
              key={e.id}
              className="rounded-lg border border-accanto-100 bg-white p-3"
            >
              <Text className="text-sm text-accanto-900">
                <Text className="font-medium">
                  {e.performedByDisplayName ?? t('audit.removedMember')}
                </Text>
                {' — '}
                <Text>{t(`audit.actions.${e.actionType}`, { defaultValue: e.actionType })}</Text>
              </Text>
              {e.summary ? (
                <Text className="text-sm text-accanto-700 mt-1">
                  {e.summary}
                </Text>
              ) : null}
              <Text className="text-xs text-accanto-500 mt-1">
                {new Date(e.timestamp).toLocaleString(i18n.language)}
              </Text>
            </View>
          ))}
        </View>
      )}

      {skip < total ? (
        <View className="mt-4">
          <Button
            variant="ghost"
            onPress={() => load(skip, false)}
            disabled={loading}
            busy={loading}
          >
            {loading ? t('common.loading') : t('audit.loadMore')}
          </Button>
        </View>
      ) : null}
      {loading && items.length === 0 ? (
        <ActivityIndicator color="#334155" />
      ) : null}
    </Screen>
  );
}
