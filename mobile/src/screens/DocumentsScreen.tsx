import { useCallback, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Pressable,
  Text,
  View
} from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import * as DocumentPicker from 'expo-document-picker';
import * as FileSystem from 'expo-file-system/legacy';
import * as Sharing from 'expo-sharing';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import TextField from '../components/ui/TextField';
import SelectField from '../components/ui/SelectField';
import ErrorBanner from '../components/ui/ErrorBanner';
import { api, extractError } from '../api/client';
import { getToken } from '../storage/secureStorage';
import { API_BASE_URL } from '../config/env';
import { useCircleId } from '../navigation/CircleContext';
import type { DocumentCategory, DocumentItem } from '@accanto/shared/types';
import { DocumentCategoryLabel } from '@accanto/shared/types';

const CATS: DocumentCategory[] = [
  'Report',
  'BloodTest',
  'Imaging',
  'Prescription',
  'Therapy',
  'IdentityDocument',
  'Delegation',
  'HospitalContact',
  'Other'
];

function formatSize(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(0)} KB`;
  return `${(n / 1024 / 1024).toFixed(1)} MB`;
}

// Sanitizza il nome file per evitare path traversal scrivendo in cache.
function safeFilename(name: string): string {
  return name.replace(/[^\w\-. ]+/g, '_').slice(0, 200) || 'documento';
}

export default function DocumentsScreen() {
  const circleId = useCircleId();
  const [docs, setDocs] = useState<DocumentItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const { data } = await api.get<DocumentItem[]>(
        `/care-circles/${circleId}/documents`
      );
      setDocs(data);
    } catch (e) {
      setError(extractError(e));
      setDocs([]);
    }
  }, [circleId]);

  useFocusEffect(
    useCallback(() => {
      load();
    }, [load])
  );

  const download = async (d: DocumentItem) => {
    setError(null);
    setDownloadingId(d.id);
    try {
      const token = await getToken();
      if (!token) {
        setError('Sessione scaduta, accedi di nuovo.');
        return;
      }
      const url = `${API_BASE_URL}/care-circles/${circleId}/documents/${d.id}/download`;
      const dst = `${FileSystem.cacheDirectory}${safeFilename(d.originalFileName)}`;
      const res = await FileSystem.downloadAsync(url, dst, {
        headers: { Authorization: `Bearer ${token}` }
      });
      if (res.status >= 400) {
        setError('Errore durante il download.');
        return;
      }
      if (await Sharing.isAvailableAsync()) {
        await Sharing.shareAsync(res.uri, {
          mimeType: d.contentType || undefined,
          dialogTitle: d.originalFileName,
          UTI: undefined
        });
      } else {
        Alert.alert(
          'Download completato',
          `Salvato in: ${res.uri}`
        );
      }
    } catch (e) {
      setError(extractError(e));
    } finally {
      setDownloadingId(null);
    }
  };

  const del = (d: DocumentItem) => {
    Alert.alert(
      `Eliminare "${d.originalFileName}"?`,
      'Il file non potrà essere recuperato.',
      [
        { text: 'Annulla', style: 'cancel' },
        {
          text: 'Elimina',
          style: 'destructive',
          onPress: async () => {
            try {
              await api.delete(
                `/care-circles/${circleId}/documents/${d.id}`
              );
              load();
            } catch (e) {
              setError(extractError(e));
            }
          }
        }
      ]
    );
  };

  return (
    <Screen>
      <Text className="text-2xl font-semibold text-accanto-900 mb-1">
        Documenti
      </Text>
      <Text className="text-accanto-500 mb-4">
        Tieni vicino ciò che ti serve quando ti chiedono un documento.
      </Text>

      <View className="mb-4">
        <Button onPress={() => setShowForm((s) => !s)}>
          {showForm ? 'Annulla' : '+ Carica documento'}
        </Button>
      </View>

      {showForm ? (
        <UploadForm
          circleId={circleId}
          onUploaded={() => {
            setShowForm(false);
            load();
          }}
        />
      ) : null}

      <View className="mb-3">
        <ErrorBanner message={error} />
      </View>

      {docs === null ? (
        <View className="py-6 items-center">
          <ActivityIndicator color="#334155" />
        </View>
      ) : docs.length === 0 ? (
        <Text className="text-accanto-500">Ancora nessun documento.</Text>
      ) : (
        <View className="gap-3">
          {docs.map((d) => (
            <View
              key={d.id}
              className="rounded-lg border border-accanto-100 bg-white p-4"
            >
              <Text
                className="font-medium text-accanto-900"
                numberOfLines={2}
              >
                {d.originalFileName}
              </Text>
              <Text className="text-xs text-accanto-500 mt-0.5">
                {DocumentCategoryLabel[d.category]} •{' '}
                {formatSize(d.sizeInBytes)} •{' '}
                {new Date(d.createdAt).toLocaleDateString('it-IT')}
              </Text>
              {d.notes ? (
                <Text className="text-sm text-accanto-900 mt-2">
                  {d.notes}
                </Text>
              ) : null}
              {d.tags.length > 0 ? (
                <View className="mt-2 flex-row flex-wrap gap-1">
                  {d.tags.map((t) => (
                    <View
                      key={t}
                      className="bg-accanto-100 rounded px-2 py-0.5"
                    >
                      <Text className="text-xs text-accanto-700">{t}</Text>
                    </View>
                  ))}
                </View>
              ) : null}
              <View className="mt-3 flex-row gap-2">
                <View className="flex-1">
                  <Button
                    variant="ghost"
                    onPress={() => download(d)}
                    busy={downloadingId === d.id}
                    disabled={downloadingId === d.id}
                  >
                    {downloadingId === d.id ? 'Apertura…' : 'Apri'}
                  </Button>
                </View>
                <Pressable
                  onPress={() => del(d)}
                  className="px-3 py-2 items-center justify-center"
                >
                  <Text className="text-sm text-accanto-500">Elimina</Text>
                </Pressable>
              </View>
            </View>
          ))}
        </View>
      )}
    </Screen>
  );
}

function UploadForm({
  circleId,
  onUploaded
}: {
  circleId: string;
  onUploaded: () => void;
}) {
  const [file, setFile] = useState<DocumentPicker.DocumentPickerAsset | null>(
    null
  );
  const [category, setCategory] = useState<DocumentCategory>('Report');
  const [notes, setNotes] = useState('');
  const [tags, setTags] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const catOptions = useMemo(
    () => CATS.map((c) => ({ value: c, label: DocumentCategoryLabel[c] })),
    []
  );

  const pickFile = async () => {
    setError(null);
    try {
      const res = await DocumentPicker.getDocumentAsync({
        copyToCacheDirectory: true,
        multiple: false
      });
      if (res.canceled || !res.assets[0]) return;
      const asset = res.assets[0];
      // 20 MB limite (allineato al backend del web).
      if (asset.size && asset.size > 20 * 1024 * 1024) {
        setError('Il file supera i 20 MB.');
        return;
      }
      setFile(asset);
    } catch (e) {
      setError(extractError(e));
    }
  };

  const submit = async () => {
    if (!file) {
      setError('Scegli un file.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const fd = new FormData();
      // RN-style file append per axios + multipart/form-data.
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      fd.append('file', {
        uri: file.uri,
        name: file.name,
        type: file.mimeType ?? 'application/octet-stream'
      } as any);
      fd.append('category', category);
      if (notes.trim()) fd.append('notes', notes.trim());
      if (tags.trim()) fd.append('tags', tags.trim());

      await api.post(`/care-circles/${circleId}/documents`, fd, {
        headers: { 'Content-Type': 'multipart/form-data' }
      });
      onUploaded();
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <View className="rounded-lg border border-accanto-100 bg-white p-4 mb-4 gap-3">
      <View>
        <Text className="text-sm font-medium text-accanto-700 mb-1">
          File
        </Text>
        <Pressable
          onPress={pickFile}
          className="rounded-md border border-accanto-100 bg-white px-3 py-3"
        >
          <Text
            className={`text-base ${
              file ? 'text-accanto-900' : 'text-accanto-500'
            }`}
            numberOfLines={1}
          >
            {file ? file.name : 'Tocca per scegliere un file…'}
          </Text>
        </Pressable>
        <Text className="text-xs text-accanto-500 mt-1">Massimo 20 MB.</Text>
      </View>

      <SelectField
        label="Categoria"
        value={category}
        onChange={(v) => v && setCategory(v as DocumentCategory)}
        options={catOptions}
      />

      <TextField
        label="Note (facoltative)"
        value={notes}
        onChangeText={setNotes}
        multiline
        numberOfLines={3}
        style={{ minHeight: 60, textAlignVertical: 'top' }}
      />

      <TextField
        label="Tag (separati da virgola)"
        value={tags}
        onChangeText={setTags}
        autoCapitalize="none"
        autoCorrect={false}
      />

      <ErrorBanner message={error} />

      <Button onPress={submit} busy={busy} disabled={busy || !file}>
        {busy ? 'Caricamento…' : 'Carica'}
      </Button>
    </View>
  );
}
