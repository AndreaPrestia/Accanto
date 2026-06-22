// SDK 54 espone una API modulare nuova (Paths/Directory/File); manteniamo
// l'API classica via il sub-path /legacy finché non migriamo i call site.
import * as FileSystem from 'expo-file-system/legacy';
import * as Sharing from 'expo-sharing';
import { getToken } from '../storage/secureStorage';
import { API_BASE_URL } from '../config/env';

function safeFilename(name: string): string {
  return name.replace(/[^\w\-. ]+/g, '_').slice(0, 200) || 'download';
}

export interface DownloadOptions {
  /** Path API relativo (es. `/account/export`) oppure URL assoluto. */
  path: string;
  /** Nome file di fallback quando il server non lo invia. */
  fallbackFilename: string;
  /** Mime type per il share sheet (es. `application/pdf`). */
  mimeType?: string;
  /** Titolo del dialog di share. */
  dialogTitle?: string;
}

/**
 * Scarica una risorsa API protetta in cache e la passa al share sheet di
 * sistema (su iOS: AirDrop/Files/etc.; su Android: scegli app). Su web o se
 * lo share non è disponibile il file resta in cacheDirectory.
 *
 * Allega l'access token corrente perché `FileSystem.downloadAsync` non passa
 * dall'interceptor di axios.
 */
export async function downloadAndShare(opts: DownloadOptions): Promise<string> {
  const token = await getToken();
  if (!token) {
    throw new Error('Sessione scaduta, accedi di nuovo.');
  }
  const url = opts.path.startsWith('http')
    ? opts.path
    : `${API_BASE_URL}${opts.path}`;
  const dst = `${FileSystem.cacheDirectory}${safeFilename(opts.fallbackFilename)}`;
  const res = await FileSystem.downloadAsync(url, dst, {
    headers: { Authorization: `Bearer ${token}` }
  });
  if (res.status >= 400) {
    throw new Error(`Download fallito (HTTP ${res.status}).`);
  }
  if (await Sharing.isAvailableAsync()) {
    await Sharing.shareAsync(res.uri, {
      mimeType: opts.mimeType,
      dialogTitle: opts.dialogTitle ?? opts.fallbackFilename
    });
  }
  return res.uri;
}
