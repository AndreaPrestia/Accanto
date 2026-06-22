import { useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import ErrorBanner from '../ui/ErrorBanner';
import { api, extractError } from '../../api/client';
import type {
  PagedResult,
  SecurityAuditEntry
} from '@accanto/shared/types';

const PAGE_SIZE = 20;

export default function SecurityAuditSection() {
  const { t, i18n } = useTranslation();
  const [skip, setSkip] = useState(0);
  const [data, setData] = useState<PagedResult<SecurityAuditEntry> | null>(
    null
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async (newSkip = skip) => {
    setBusy(true);
    setError(null);
    try {
      const { data } = await api.get<PagedResult<SecurityAuditEntry>>(
        '/account/security-audit',
        { params: { skip: newSkip, take: PAGE_SIZE } }
      );
      setData(data);
      setSkip(newSkip);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  };

  useEffect(() => {
    load(0);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const fmt = (iso: string) => {
    try {
      return new Date(iso).toLocaleString(i18n.language);
    } catch {
      return iso;
    }
  };

  const eventLabel = (type: string) =>
    t(`account.securityAudit.events.${type}`, { defaultValue: type }) as string;

  const total = data?.total ?? 0;
  const hasPrev = skip > 0;
  const hasNext = data ? skip + data.items.length < total : false;

  return (
    <View className="gap-3">
      <Text className="text-base font-semibold text-accanto-900">
        {t('account.securityAudit.title')}
      </Text>
      <Text className="text-sm text-accanto-500">
        {t('account.securityAudit.hint')}
      </Text>
      <ErrorBanner message={error} />

      {busy && !data ? (
        <View className="py-2">
          <ActivityIndicator color="#334155" />
        </View>
      ) : data && data.items.length === 0 ? (
        <Text className="text-sm text-accanto-500">
          {t('account.securityAudit.empty')}
        </Text>
      ) : (
        <View className="gap-2">
          {data?.items.map((e) => (
            <View
              key={e.id}
              className="rounded-lg border border-accanto-100 bg-white p-3"
            >
              <View className="flex-row items-baseline justify-between gap-2">
                <Text className="text-sm font-medium text-accanto-900 flex-1">
                  {eventLabel(e.eventType)}
                </Text>
                <Text className="text-xs text-accanto-500">
                  {fmt(e.timestamp)}
                </Text>
              </View>
              {e.summary ? (
                <Text className="text-sm text-accanto-700 mt-1">
                  {e.summary}
                </Text>
              ) : null}
              {e.ipAddress || e.userAgent ? (
                <Text className="text-xs text-accanto-500 mt-1">
                  {e.ipAddress}
                  {e.ipAddress && e.userAgent ? ' \u00b7 ' : ''}
                  {e.userAgent}
                </Text>
              ) : null}
            </View>
          ))}
        </View>
      )}

      <View className="flex-row gap-2">
        <Pressable
          onPress={() => load(Math.max(0, skip - PAGE_SIZE))}
          disabled={!hasPrev || busy}
          className={`px-3 py-1.5 rounded-lg border border-accanto-100 ${
            !hasPrev || busy ? 'opacity-50' : ''
          }`}
        >
          <Text className="text-sm text-accanto-700">
            {t('common.previous', { defaultValue: '\u2190 Precedente' }) as string}
          </Text>
        </Pressable>
        <Pressable
          onPress={() => load(skip + PAGE_SIZE)}
          disabled={!hasNext || busy}
          className={`px-3 py-1.5 rounded-lg border border-accanto-100 ${
            !hasNext || busy ? 'opacity-50' : ''
          }`}
        >
          <Text className="text-sm text-accanto-700">
            {t('common.next', { defaultValue: 'Successivo \u2192' }) as string}
          </Text>
        </Pressable>
      </View>
    </View>
  );
}
