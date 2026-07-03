import './global.css';
import { useEffect, useState } from 'react';
import { ActivityIndicator, AppState, View } from 'react-native';
import * as Notifications from 'expo-notifications';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { StatusBar } from 'expo-status-bar';
import {
  useFonts,
  Inter_400Regular,
  Inter_600SemiBold,
  Inter_700Bold
} from '@expo-google-fonts/inter';
import { initI18n } from './src/i18n';
import { AuthProvider } from './src/auth/AuthContext';
import RootNavigator from './src/navigation/RootNavigator';
import {
  configureForegroundHandler,
  incrementBadgeAsync,
  resetBadgeAsync
} from './src/lib/push';

// Handler globale per la visualizzazione delle push in foreground.
// Va eseguito una volta sola, fuori dal componente, perché
// setNotificationHandler è un side-effect a livello modulo.
configureForegroundHandler();

export default function App() {
  const [i18nReady, setI18nReady] = useState(false);
  const [fontsLoaded] = useFonts({
    Inter_400Regular,
    Inter_600SemiBold,
    Inter_700Bold
  });

  useEffect(() => {
    initI18n()
      .then(() => setI18nReady(true))
      .catch(() => setI18nReady(true)); // best-effort: avvia comunque
  }, []);

  /**
   * Gestione del badge sull'icona app:
   * - quando arriva una push (foreground o background) incrementiamo di 1
   *   per dare feedback visivo del contenuto non letto;
   * - quando l'utente torna sull'app (AppState 'active') azzeriamo il
   *   badge, perché la prossima sessione è considerata "letta".
   * Note: shouldSetBadge: true nel foreground handler permette al payload
   * Expo di scavalcare il counter manuale quando avrà il campo `badge`.
   */
  useEffect(() => {
    const receivedSub = Notifications.addNotificationReceivedListener(() => {
      incrementBadgeAsync();
    });
    const appStateSub = AppState.addEventListener('change', (state) => {
      if (state === 'active') {
        resetBadgeAsync();
      }
    });
    // Reset una volta all'avvio (l'utente sta aprendo l'app -> sta leggendo).
    resetBadgeAsync();
    return () => {
      receivedSub.remove();
      appStateSub.remove();
    };
  }, []);

  if (!fontsLoaded || !i18nReady) {
    return (
      <View className="flex-1 items-center justify-center bg-accanto-50">
        <ActivityIndicator size="large" color="#334155" />
      </View>
    );
  }

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SafeAreaProvider>
        <StatusBar style="dark" />
        <AuthProvider>
          <RootNavigator />
        </AuthProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}
