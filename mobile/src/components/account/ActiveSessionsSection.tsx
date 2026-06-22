import { useEffect, useState } from 'react';
import {
  ActivityIndicator,
  Pressable,
  Text,
  View
} from 'react-native';
import { useTranslation } from 'react-i18next';
import ErrorBanner from '../ui/ErrorBanner';
import { api, extractError } from '../../api/client';
import { getRefreshToken } from '../../storage/secureStorage';
import type { ActiveSession } from '@accanto/shared/types';

function formatDate(value: string, locale: string): string {
  try {
    return new Date(value).toLocaleString(locale);
  } catch {
    return value;
  }
}

function shortenUserAgent(
  ua: string | null | undefined,
  fallback: string
): string {
  if (!ua) return fallback;
  const browser =
    /Edg\/([\d.]+)/.exec(ua)?.[0].replace('Edg/', 'Edge ') ||
    /Firefox\/([\d.]+)/.exec(ua)?.[0].replace('/', ' ') ||
    /Chrome\/([\d.]+)/.exec(ua)?.[0].replace('/', ' ') ||
    /Safari\/([\d.]+)/.exec(ua)?.[0].replace('/', ' ') ||
    ua.slice(0, 80);
  const os =
    /Windows NT [\d.]+/.exec(ua)?.[0] ||
    /Mac OS X [\d_.]+/.exec(ua)?.[0]?.replace(/_/g, '.') ||
    /Android [\d.]+/.exec(ua)?.[0] ||
    /iPhone OS [\d_]+/.exec(ua)?.[0]?.replace(/_/g, '.') ||
    /Linux/.exec(ua)?.[0] ||
    '';
  return [browser, os].filter(Boolean).join(' \u00b7 ') || ua.slice(0, 80);
}

export default function ActiveSessionsSection() {
  const { t, i18n } = useTranslation();
  const [sessions, setSessions] = useState<ActiveSession[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [revoking, setRevoking] = useState<string | null>(null);

  const load = async () => {
    setError(null);
    setLoading(true);
    try {
      const refreshToken = (await getRefreshToken()) ?? '';
      const res = await api.get<ActiveSession[]>('/account/sessions', {
        params: refreshToken ? { current: refreshToken } : undefined
      });
      setSessions(res.data);
    } catch (e) {
      setError(extractError(e) || (t('account.sessionsLoadError') as string));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const revoke = async (id: string) => {
    setRevoking(id);
    setError(null);
    try {
      await api.delete(`/account/sessions/${id}`);
      setSessions((prev) => prev?.filter((s) => s.id !== id) ?? null);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setRevoking(null);
    }
  };

  const locale = i18n.language || 'it';

  return (
    <View className="gap-3">
      <Text className="text-base font-semibold text-accanto-900">
        {t('account.sessionsTitle')}
      </Text>
      <Text className="text-sm text-accanto-500">
        {t('account.sessionsHint')}
      </Text>
      <ErrorBanner message={error} />
      {loading ? (
        <View className="py-2">
          <ActivityIndicator color="#334155" />
        </View>
      ) : sessions && sessions.length === 0 ? (
        <Text className="text-sm text-accanto-500">
          {t('account.sessionsEmpty')}
        </Text>
      ) : (
        <View className="gap-2">
          {(sessions ?? []).map((s) => (
            <View
              key={s.id}
              className="border border-accanto-100 rounded-lg p-3 gap-2"
            >
              <View className="flex-row items-center gap-2 flex-wrap">
                <Text className="font-medium text-accanto-900 flex-1">
                  {shortenUserAgent(
                    s.userAgent,
                    t('account.sessionsUnknownDevice') as string
                  )}
                </Text>
                {s.current ? (
                  <View className="bg-green-100 rounded px-2 py-0.5">
                    <Text className="text-xs text-green-800">
                      {t('account.sessionsCurrent')}
                    </Text>
                  </View>
                ) : null}
              </View>
              <Text className="text-xs text-accanto-500">
                {t('account.sessionsCreatedAt')}:{' '}
                {formatDate(s.createdAt, locale)}
                {' \u00b7 '}
                {t('account.sessionsExpiresAt')}:{' '}
                {formatDate(s.expiresAt, locale)}
                {s.ipAddress ? ` \u00b7 ${s.ipAddress}` : ''}
              </Text>
              {!s.current ? (
                <Pressable
                  onPress={() => revoke(s.id)}
                  disabled={revoking === s.id}
                  className="self-start px-3 py-1.5 rounded-lg border border-red-300"
                >
                  <Text className="text-sm text-red-700">
                    {revoking === s.id
                      ? (t('account.sessionsRevoking') as string)
                      : (t('account.sessionsRevoke') as string)}
                  </Text>
                </Pressable>
              ) : null}
            </View>
          ))}
        </View>
      )}
    </View>
  );
}
