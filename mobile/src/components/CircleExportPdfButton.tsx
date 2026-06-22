import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import Button from './ui/Button';
import DateField from './ui/DateField';
import ErrorBanner from './ui/ErrorBanner';
import { downloadAndShare } from '../lib/download';

interface Props {
  circleId: string;
  circleName?: string;
}

/**
 * Bottone "Esporta in PDF" per un cerchio. Optional filtri di data, share
 * sheet di sistema per salvare/inviare il PDF generato dal backend.
 */
export default function CircleExportPdfButton({ circleId, circleName }: Props) {
  const [from, setFrom] = useState<string>('');
  const [to, setTo] = useState<string>('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const start = async () => {
    setBusy(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      if (from) {
        const d = new Date(from);
        d.setHours(0, 0, 0, 0);
        params.set('from', d.toISOString());
      }
      if (to) {
        const d = new Date(to);
        d.setHours(23, 59, 59, 999);
        params.set('to', d.toISOString());
      }
      const qs = params.toString();
      const path = `/care-circles/${circleId}/export/pdf${qs ? `?${qs}` : ''}`;
      const filename = circleName
        ? `accanto-${circleName.replace(/[^\w\-]+/g, '_')}.pdf`
        : 'accanto-cerchio.pdf';
      await downloadAndShare({
        path,
        fallbackFilename: filename,
        mimeType: 'application/pdf',
        dialogTitle: 'Esporta in PDF'
      });
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <View className="rounded-lg border border-accanto-100 bg-white p-4 gap-3">
      <View className="gap-1">
        <Text className="font-medium text-accanto-900">Esporta in PDF</Text>
        <Text className="text-sm text-accanto-500">
          Un riassunto del cerchio (diario e domande aperte) da portare al
          medico.
        </Text>
      </View>
      <View className="flex-row gap-2">
        <View className="flex-1">
          <DateField
            label="Dal"
            value={from}
            onChange={setFrom}
            maximumDate={to || undefined}
            clearable
          />
        </View>
        <View className="flex-1">
          <DateField
            label="Al"
            value={to}
            onChange={setTo}
            minimumDate={from || undefined}
            clearable
          />
        </View>
      </View>
      <ErrorBanner message={error} />
      <View className="flex-row items-center gap-3">
        <View className="flex-1">
          <Button onPress={start} busy={busy} disabled={busy}>
            {busy ? 'Generazione\u2026' : 'Scarica PDF'}
          </Button>
        </View>
        {from || to ? (
          <Pressable
            onPress={() => {
              setFrom('');
              setTo('');
            }}
          >
            <Text className="text-sm text-accanto-700 underline">
              Pulisci filtri
            </Text>
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}
