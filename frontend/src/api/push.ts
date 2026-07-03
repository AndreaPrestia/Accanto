import { api } from './client';

function urlBase64ToUint8Array(base64: string): Uint8Array {
  const padding = '='.repeat((4 - (base64.length % 4)) % 4);
  const safe = (base64 + padding).replace(/-/g, '+').replace(/_/g, '/');
  const raw = atob(safe);
  const out = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) out[i] = raw.charCodeAt(i);
  return out;
}

/**
 * Confronto byte-a-byte fra la chiave VAPID corrente del server e quella
 * memorizzata nella subscription esistente del browser. Serve per capire
 * se la subscription cachata e' stata creata con la chiave attuale o con
 * una vecchia (in tal caso va dismessa prima di crearne una nuova,
 * altrimenti pushManager.subscribe() ritorna NotAllowedError
 * "Registration failed - permission denied").
 */
function sameKey(a: ArrayBuffer | null | undefined, b: Uint8Array): boolean {
  if (!a) return false;
  const av = new Uint8Array(a);
  if (av.length !== b.length) return false;
  for (let i = 0; i < av.length; i++) if (av[i] !== b[i]) return false;
  return true;
}

export function isPushSupported(): boolean {
  return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
}

export async function getPushPermission(): Promise<NotificationPermission> {
  return Notification.permission;
}

export async function getExistingSubscription(): Promise<PushSubscription | null> {
  if (!isPushSupported()) return null;
  const reg = await navigator.serviceWorker.ready;
  return reg.pushManager.getSubscription();
}

export async function subscribeToPush(): Promise<PushSubscription> {
  if (!isPushSupported()) throw new Error('Notifiche push non supportate da questo browser.');

  const permission = await Notification.requestPermission();
  if (permission !== 'granted') {
    throw new Error(
      `Permesso per le notifiche non concesso (stato: ${permission}). ` +
        'Sblocca le notifiche per questo sito dalle impostazioni del browser e riprova.'
    );
  }

  const keyRes = await api.get<{ publicKey: string }>('/push/vapid-public-key');
  const publicKey = keyRes.data.publicKey;
  if (!publicKey) throw new Error('Notifiche non configurate sul server.');
  const applicationServerKey = urlBase64ToUint8Array(publicKey);
  // Cast esplicito ad ArrayBuffer: in lib.dom.d.ts pi\u00f9 recenti
  // PushSubscriptionOptionsInit.applicationServerKey accetta solo
  // BufferSource con ArrayBuffer "puro", non Uint8Array<ArrayBufferLike>
  // (che potrebbe in teoria essere un SharedArrayBuffer). Passare .buffer
  // \u00e8 sempre stato il modo standard di passare la chiave VAPID.
  const appServerKeyBuffer = applicationServerKey.buffer as ArrayBuffer;

  const reg = await navigator.serviceWorker.ready;
  let sub = await reg.pushManager.getSubscription();

  // Se la subscription esistente e' stata creata con una chiave VAPID
  // diversa (cambio di server, rigenerazione chiavi, residuo da test
  // precedenti), dobbiamo rimuoverla prima di chiederne una nuova. Il
  // browser non permette due subscription attive con chiavi diverse
  // sullo stesso Service Worker e fallirebbe con
  // "Registration failed - permission denied".
  if (sub && !sameKey(sub.options.applicationServerKey, applicationServerKey)) {
    try {
      await api.post('/push/unsubscribe', { endpoint: sub.endpoint });
    } catch {
      // best-effort: continuiamo anche se il backend non conosce piu' l'endpoint
    }
    await sub.unsubscribe();
    sub = null;
  }

  if (!sub) {
    try {
      sub = await reg.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: appServerKeyBuffer
      });
    } catch (err) {
      const name = (err as { name?: string })?.name ?? 'Error';
      const msg = (err as Error)?.message ?? String(err);
      throw new Error(
        `Impossibile registrare il dispositivo per le notifiche (${name}: ${msg}). ` +
          'Prova a chiudere e riaprire il browser, oppure ad annullare la registrazione del Service Worker da DevTools \u2192 Application.'
      );
    }
  }

  const json = sub.toJSON();
  await api.post('/push/subscribe', {
    endpoint: sub.endpoint,
    p256dh: json.keys?.p256dh ?? '',
    auth: json.keys?.auth ?? '',
    userAgent: navigator.userAgent
  });
  return sub;
}

export async function unsubscribeFromPush(): Promise<void> {
  const sub = await getExistingSubscription();
  if (!sub) return;
  try {
    await api.post('/push/unsubscribe', { endpoint: sub.endpoint });
  } catch {
    // ignora: rimuoviamo comunque la sottoscrizione lato browser
  }
  await sub.unsubscribe();
}
