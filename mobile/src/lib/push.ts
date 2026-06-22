import * as Notifications from 'expo-notifications';
import * as Device from 'expo-device';
import { Platform } from 'react-native';
import Constants from 'expo-constants';
import { api } from '../api/client';

/**
 * Wrapper sopra expo-notifications per il flusso "push device":
 * - chiede il permesso utente (se non ancora deciso);
 * - ottiene l'Expo push token via getExpoPushTokenAsync (richiede
 *   projectId in app.config — passato qui esplicitamente per evitare
 *   warning con SDK 53+);
 * - registra il token presso il backend /account/push-devices;
 * - in fase di logout invia DELETE per quel token.
 *
 * Tutte le funzioni sono best-effort: in caso di errore loggano in
 * console e ritornano null/false invece di propagare l'eccezione, così
 * l'UX non viene mai bloccata da un problema di notifiche.
 */

/** Cache locale per il logout: il token attualmente registrato. */
let currentToken: string | null = null;

export function getCurrentPushToken(): string | null {
  return currentToken;
}

function getProjectId(): string | undefined {
  const eas = (Constants.expoConfig as any)?.extra?.eas?.projectId;
  if (eas) return eas;
  return (Constants.easConfig as any)?.projectId;
}

/**
 * Registra il device per le push notifications. Restituisce il token
 * Expo se l'utente ha concesso il permesso, altrimenti null.
 *
 * Va invocato DOPO il login (così l'header Authorization è popolato).
 */
export async function registerForPushNotificationsAsync(): Promise<string | null> {
  if (!Device.isDevice) {
    // Simulatori iOS/Android non ricevono push reali.
    return null;
  }

  try {
    const { status: existing } = await Notifications.getPermissionsAsync();
    let status = existing;
    if (existing !== 'granted') {
      const { status: req } = await Notifications.requestPermissionsAsync();
      status = req;
    }
    if (status !== 'granted') return null;

    if (Platform.OS === 'android') {
      // Canale di default richiesto da Android 8+
      await Notifications.setNotificationChannelAsync('default', {
        name: 'Accanto',
        importance: Notifications.AndroidImportance.DEFAULT,
        lightColor: '#0f766e'
      });
    }

    const projectId = getProjectId();
    const tokenResult = await Notifications.getExpoPushTokenAsync(
      projectId ? { projectId } : undefined
    );
    const token = tokenResult.data;
    currentToken = token;

    const deviceName =
      Device.deviceName ??
      `${Device.brand ?? 'device'} ${Device.modelName ?? ''}`.trim();

    await api.post('/account/push-devices', {
      token,
      platform: Platform.OS,
      deviceName: deviceName.length > 0 ? deviceName : null
    });

    return token;
  } catch (err) {
    // Best-effort: niente di critico se la registrazione fallisce.
    console.warn('[push] registrazione fallita', err);
    return null;
  }
}

/**
 * Da chiamare in fase di logout PRIMA che l'access token venga
 * cancellato (per autenticare il DELETE).
 */
export async function unregisterPushTokenAsync(token?: string | null): Promise<void> {
  const tk = token ?? currentToken;
  if (!tk) return;
  try {
    await api.delete('/account/push-devices', { data: { token: tk } });
  } catch (err) {
    console.warn('[push] unregister fallito', err);
  } finally {
    currentToken = null;
  }
}

/**
 * Setup globale del comportamento foreground: in iOS senza handler
 * esplicito le notifiche ricevute con l'app aperta NON mostrano l'alert.
 * Va chiamato una sola volta all'avvio.
 */
export function configureForegroundHandler() {
  Notifications.setNotificationHandler({
    handleNotification: async () => ({
      shouldShowAlert: true,
      shouldShowBanner: true,
      shouldShowList: true,
      shouldPlaySound: true,
      // true: se il payload Expo contiene `badge`, l'OS lo applica
      // automaticamente. Quando invece il backend non manda il count
      // (caso attuale), incrementiamo a mano sotto via
      // addNotificationReceivedListener -> incrementBadge().
      shouldSetBadge: true
    })
  });
}

/**
 * Incrementa di 1 il badge sull'icona app. Usato dal listener globale
 * registrato in App.tsx quando arriva una push e l'app non è attiva
 * (o il payload non porta `badge`).
 */
export async function incrementBadgeAsync(): Promise<void> {
  try {
    const current = await Notifications.getBadgeCountAsync();
    await Notifications.setBadgeCountAsync(current + 1);
  } catch (err) {
    console.warn('[push] increment badge fallito', err);
  }
}

/**
 * Azzera il badge sull'icona app. Da chiamare quando l'utente apre
 * l'app (AppState transitions a 'active') o quando ha appena letto
 * tutto il diario / le notifiche.
 */
export async function resetBadgeAsync(): Promise<void> {
  try {
    await Notifications.setBadgeCountAsync(0);
  } catch (err) {
    console.warn('[push] reset badge fallito', err);
  }
}
