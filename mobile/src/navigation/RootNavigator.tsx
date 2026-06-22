import { useEffect, useMemo } from 'react';
import { ActivityIndicator, View, Text, Pressable } from 'react-native';
import {
  NavigationContainer,
  createNavigationContainerRef,
  CommonActions
} from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import type { RootStackParamList } from './types';
import { linking } from './linking';
import AuthStack from './AuthStack';
import AppStack from './AppStack';
import { useAuth } from '../auth/AuthContext';
import { authenticateBiometric } from '../auth/biometric';
import { getPendingInvite, setPendingInvite } from '../auth/pendingInvite';

export const navigationRef = createNavigationContainerRef<RootStackParamList>();

const Stack = createNativeStackNavigator<RootStackParamList>();

function LoadingScreen() {
  return (
    <View className="flex-1 items-center justify-center bg-accanto-50">
      <ActivityIndicator size="large" color="#0f172a" />
    </View>
  );
}

function BiometricLockScreen() {
  const { unlockBiometric, logout } = useAuth();
  return (
    <View className="flex-1 items-center justify-center bg-accanto-50 px-6">
      <Text className="font-bold text-xl text-accanto-900 mb-2">Accanto è bloccato</Text>
      <Text className="text-accanto-500 text-center text-sm mb-6">
        Sblocca con il riconoscimento biometrico per continuare.
      </Text>
      <Pressable
        className="bg-accanto-900 rounded-xl px-6 py-3 mb-3"
        onPress={async () => {
          const result = await authenticateBiometric({
            promptMessage: 'Sblocca Accanto'
          });
          if (result.success) {
            unlockBiometric();
          }
        }}
      >
        <Text className="text-white font-semibold">Sblocca</Text>
      </Pressable>
      <Pressable
        className="px-6 py-3"
        onPress={() => {
          logout().catch(() => {
            /* ignore */
          });
        }}
      >
        <Text className="text-accanto-600 font-semibold">Esci</Text>
      </Pressable>
    </View>
  );
}

export default function RootNavigator() {
  const { user, loading, needsBiometricUnlock } = useAuth();
  // Memoizziamo lo stack scelto così NavigationContainer non rimonta inutilmente.
  const screens = useMemo(() => {
    if (user) {
      return (
        <Stack.Screen name="App" component={AppStack} />
      );
    }
    return (
      <Stack.Screen name="Auth" component={AuthStack} />
    );
  }, [user]);

  // Quando l'utente diventa autenticato, se c'è un invito "pending" salvato
  // (catturato da un deep link mentre era nella AuthStack), naviga su
  // App > InviteAccept con il token e poi pulisce.
  useEffect(() => {
    if (!user || needsBiometricUnlock) return;
    let cancelled = false;
    (async () => {
      const token = await getPendingInvite();
      if (cancelled || !token) return;
      if (!navigationRef.isReady()) {
        // Riprova dopo un microtask, NavigationContainer si monta async.
        setTimeout(() => {
          if (cancelled || !navigationRef.isReady()) return;
          navigationRef.dispatch(
            CommonActions.navigate({
              name: 'App',
              params: {
                screen: 'InviteAccept',
                params: { token }
              }
            })
          );
        }, 0);
      } else {
        navigationRef.dispatch(
          CommonActions.navigate({
            name: 'App',
            params: { screen: 'InviteAccept', params: { token } }
          })
        );
      }
      await setPendingInvite(null);
    })();
    return () => {
      cancelled = true;
    };
  }, [user, needsBiometricUnlock]);

  if (loading) {
    return <LoadingScreen />;
  }

  if (user && needsBiometricUnlock) {
    return <BiometricLockScreen />;
  }

  return (
    <NavigationContainer
      ref={navigationRef}
      linking={linking}
      fallback={<LoadingScreen />}
    >
      <Stack.Navigator screenOptions={{ headerShown: false }}>
        {screens}
      </Stack.Navigator>
    </NavigationContainer>
  );
}
