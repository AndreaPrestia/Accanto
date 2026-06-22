import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import Button from '../ui/Button';
import TextField from '../ui/TextField';
import ErrorBanner from '../ui/ErrorBanner';
import { api, extractError } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';

export default function DeleteAccountSection() {
  const { t } = useTranslation();
  const { logout } = useAuth();
  const [password, setPassword] = useState('');
  const [understood, setUnderstood] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setError(null);
    if (!understood) {
      setError(t('account.deleteUnderstand') as string);
      return;
    }
    setBusy(true);
    try {
      await api.delete('/account', {
        data: { currentPassword: password }
      });
      // logout pulisce token e fa scattare il redirect a AuthStack via RootNavigator.
      await logout();
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <View className="gap-3 border-t border-accanto-100 pt-6">
      <Text className="text-base font-semibold text-red-800">
        {t('account.deleteTitle')}
      </Text>
      <Text className="text-sm text-accanto-700">
        {t('account.deleteDescription')}
      </Text>
      <TextField
        label={t('account.deleteConfirmLabel')}
        value={password}
        onChangeText={setPassword}
        secureTextEntry
        autoComplete="current-password"
        textContentType="password"
      />
      <Pressable
        onPress={() => setUnderstood((u) => !u)}
        accessibilityRole="checkbox"
        accessibilityState={{ checked: understood }}
        className="flex-row items-start gap-2"
      >
        <View
          className={`w-5 h-5 rounded border ${
            understood
              ? 'bg-accanto-700 border-accanto-700'
              : 'bg-white border-accanto-100'
          } items-center justify-center mt-0.5`}
        >
          {understood ? (
            <Text className="text-white text-xs leading-none">\u2713</Text>
          ) : null}
        </View>
        <Text className="text-sm text-accanto-700 flex-1">
          {t('account.deleteUnderstand') as string}
        </Text>
      </Pressable>
      <ErrorBanner message={error} />
      <Button
        onPress={submit}
        busy={busy}
        disabled={busy || !understood}
        variant="danger"
      >
        {busy ? t('account.deleting') : t('account.deleteCta')}
      </Button>
    </View>
  );
}
