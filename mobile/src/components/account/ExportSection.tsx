import { useState } from 'react';
import { Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import Button from '../ui/Button';
import ErrorBanner from '../ui/ErrorBanner';
import { downloadAndShare } from '../../lib/download';

export default function ExportSection() {
  const { t } = useTranslation();
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const start = async () => {
    setBusy(true);
    setError(null);
    try {
      await downloadAndShare({
        path: '/account/export',
        fallbackFilename: 'accanto-export.zip',
        mimeType: 'application/zip',
        dialogTitle: t('account.exportTitle') as string
      });
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <View className="gap-3">
      <Text className="text-base font-semibold text-accanto-900">
        {t('account.exportTitle')}
      </Text>
      <Text className="text-sm text-accanto-500">
        {t('account.exportHint')}
      </Text>
      <ErrorBanner message={error} />
      <Button onPress={start} busy={busy} disabled={busy}>
        {busy ? t('account.exportPreparing') : t('account.exportCta')}
      </Button>
    </View>
  );
}
