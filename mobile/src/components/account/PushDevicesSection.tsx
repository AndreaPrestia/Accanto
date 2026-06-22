import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, Text, View } from 'react-native';
import * as Notifications from 'expo-notifications';
import ErrorBanner from '../ui/ErrorBanner';
import Button from '../ui/Button';
import { api, extractError } from '../../api/client';
import {
  registerForPushNotificationsAsync,
  unregisterPushTokenAsync,
  getCurrentPushToken
} from '../../lib/push';

interface DevicePushToken {
  id: string;
  token: string;
  platform: string;
  deviceName: string | null;
  createdAt: string;
  lastUsedAt: string;
}

function formatDate(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleString();
  } catch {
    return iso;
  }
}

/**
 * Sezione "Dispositivi che ricevono notifiche": lista i token Expo
 * registrati per l'utente corrente, permette di registrare il device
 * attuale (se il permesso è stato negato in passato) e di rimuovere
 * device specifici (es. vecchio telefono).
 */
export default function PushDevicesSection() {
  const [devices, setDevices] = useState<DevicePushToken[] | null>(null);
  const [permission, setPermission] = useState<Notifications.PermissionStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [msg, setMsg] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const { data } = await api.get<DevicePushToken[]>('/account/push-devices');
      setDevices(data);
      const perm = await Notifications.getPermissionsAsync();
      setPermission(perm.status);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const enable = async () => {
    setBusy(true);
    setError(null);
    setMsg(null);
    try {
      const token = await registerForPushNotificationsAsync();
      if (token) {
        setMsg('Notifiche attivate su questo dispositivo.');
        await load();
      } else {
        setMsg('Permesso non concesso. Abilita le notifiche dalle impostazioni del sistema.');
        const perm = await Notifications.getPermissionsAsync();
        setPermission(perm.status);
      }
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  };

  const removeDevice = (d: DevicePushToken) => {
    Alert.alert(
      'Rimuovere questo dispositivo?',
      d.deviceName ?? d.platform,
      [
        { text: 'Annulla', style: 'cancel' },
        {
          text: 'Rimuovi',
          style: 'destructive',
          onPress: async () => {
            setBusy(true);
            setError(null);
            try {
              await api.delete(`/account/push-devices/${d.id}`);
              // Se sto rimuovendo il device corrente, pulisco la cache
              // locale così il prossimo logout non manda DELETE inutile.
              if (d.token === getCurrentPushToken()) {
                await unregisterPushTokenAsync(d.token);
              }
              await load();
              setMsg('Dispositivo rimosso.');
            } catch (e) {
              setError(extractError(e));
            } finally {
              setBusy(false);
            }
          }
        }
      ]
    );
  };

  const list = devices ?? [];
  const currentToken = getCurrentPushToken();
  const hasCurrentDevice = currentToken
    ? list.some((d) => d.token === currentToken)
    : false;

  return (
    <View className="gap-3 border-t border-accanto-100 pt-6">
      <Text className="text-base font-semibold text-accanto-900">
        Notifiche push
      </Text>
      <Text className="text-sm text-accanto-700">
        Dispositivi sui quali questo account riceve notifiche push.
      </Text>

      {loading ? (
        <View className="py-2">
          <ActivityIndicator color="#334155" />
        </View>
      ) : (
        <>
          {!hasCurrentDevice ? (
            <View className="gap-2">
              <Text className="text-sm text-accanto-500">
                Questo dispositivo non è ancora registrato per ricevere notifiche.
              </Text>
              <Button onPress={enable} busy={busy} disabled={busy}>
                Attiva su questo dispositivo
              </Button>
              {permission === 'denied' ? (
                <Text className="text-xs text-amber-700">
                  Hai negato il permesso in passato: per attivarle apri le
                  impostazioni di sistema dell'app.
                </Text>
              ) : null}
            </View>
          ) : null}

          {list.length === 0 ? (
            <Text className="text-sm text-accanto-500">
              Nessun dispositivo registrato.
            </Text>
          ) : (
            <View className="gap-2">
              {list.map((d) => {
                const isCurrent = d.token === currentToken;
                return (
                  <View
                    key={d.id}
                    className="flex-row items-center justify-between gap-3 border border-accanto-100 rounded-lg px-3 py-3"
                  >
                    <View className="flex-1 gap-0.5">
                      <Text className="text-sm font-semibold text-accanto-900">
                        {d.deviceName ?? d.platform}
                        {isCurrent ? '  · Questo dispositivo' : ''}
                      </Text>
                      <Text className="text-xs text-accanto-500">
                        {d.platform} · ultimo uso {formatDate(d.lastUsedAt)}
                      </Text>
                    </View>
                    <Pressable
                      onPress={() => removeDevice(d)}
                      disabled={busy}
                      className="px-3 py-2 rounded-lg border border-rose-200"
                    >
                      <Text className="text-sm text-rose-700">Rimuovi</Text>
                    </Pressable>
                  </View>
                );
              })}
            </View>
          )}
        </>
      )}

      <ErrorBanner message={error} />
      {msg ? <Text className="text-sm text-accanto-700">{msg}</Text> : null}
    </View>
  );
}
