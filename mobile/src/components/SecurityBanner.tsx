import { useCallback, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useFocusEffect, useNavigation } from '@react-navigation/native';
import type { DrawerNavigationProp } from '@react-navigation/drawer';
import { useTranslation } from 'react-i18next';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { api } from '../api/client';
import type { TwoFactorStatus } from '@accanto/shared/types';
import type { AppDrawerParamList } from '../navigation/types';

/**
 * Banner dismissibile in cima alla Dashboard che invita ad attivare la
 * verifica in due passaggi. Compare solo se:
 *  - l'utente NON ha 2FA attiva
 *  - non ha dismesso il banner negli ultimi 30 giorni (persistenza AsyncStorage).
 *
 * Il pulsante "Attiva ora" apre lo screen `Account` del drawer.
 *
 * Usa `useFocusEffect` invece di `useEffect` così, quando l'utente attiva
 * 2FA in Account e torna alla Dashboard, il banner rifà la fetch e sparisce
 * senza aspettare un remount (React Navigation caching mantiene lo screen).
 */
const DISMISS_KEY = 'accanto.securityBanner.dismissedAt';
const DISMISS_WINDOW_MS = 30 * 24 * 60 * 60 * 1000; // 30 giorni

type Nav = DrawerNavigationProp<AppDrawerParamList>;

export default function SecurityBanner() {
  const { t } = useTranslation();
  const navigation = useNavigation<Nav>();
  const [show, setShow] = useState(false);

  useFocusEffect(
    useCallback(() => {
      let cancelled = false;
      (async () => {
        try {
          const raw = await AsyncStorage.getItem(DISMISS_KEY);
          if (raw) {
            const ts = Number(raw);
            if (Number.isFinite(ts) && Date.now() - ts < DISMISS_WINDOW_MS) {
              if (!cancelled) setShow(false);
              return;
            }
          }
          const { data } = await api.get<TwoFactorStatus>('/account/2fa');
          if (!cancelled) setShow(!data.enabled);
        } catch {
          // Silenzioso: se AsyncStorage o l'endpoint falliscono, non
          // aggiungiamo rumore extra sulla Dashboard.
        }
      })();
      return () => {
        cancelled = true;
      };
    }, [])
  );

  if (!show) return null;

  const dismiss = async () => {
    try {
      await AsyncStorage.setItem(DISMISS_KEY, String(Date.now()));
    } catch {
      /* fallito il persist: il banner tornerà al prossimo mount, pazienza */
    }
    setShow(false);
  };

  return (
    <View className="mb-4 rounded-lg border border-amber-200 bg-amber-50 p-4">
      <Text className="text-sm font-semibold text-amber-900">
        {t('security.banner.title')}
      </Text>
      <Text className="text-sm text-amber-800 mt-1">
        {t('security.banner.body')}
      </Text>
      <View className="mt-3 flex-row items-center gap-2">
        <Pressable
          onPress={() => navigation.navigate('Account')}
          accessibilityRole="button"
          className="rounded-md bg-amber-700 px-3 py-2 active:opacity-80"
        >
          <Text className="text-sm font-semibold text-white">
            {t('security.banner.cta')}
          </Text>
        </Pressable>
        <Pressable
          onPress={dismiss}
          accessibilityRole="button"
          className="px-3 py-2 active:opacity-70"
        >
          <Text className="text-sm text-amber-800 underline">
            {t('security.banner.dismiss')}
          </Text>
        </Pressable>
      </View>
    </View>
  );
}
