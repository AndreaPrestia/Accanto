import { useState } from 'react';
import { Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import Button from '../ui/Button';
import TextField from '../ui/TextField';
import ErrorBanner from '../ui/ErrorBanner';
import { api, extractError } from '../../api/client';

export default function ChangePasswordSection() {
  const { t } = useTranslation();
  const [currentPwd, setCurrentPwd] = useState('');
  const [newPwd, setNewPwd] = useState('');
  const [newPwd2, setNewPwd2] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setMessage(null);
    setError(null);
    if (newPwd !== newPwd2) {
      setError(t('account.passwordsDoNotMatch'));
      return;
    }
    if (newPwd.length < 8) {
      setError(t('account.passwordHint'));
      return;
    }
    setBusy(true);
    try {
      await api.post('/account/change-password', {
        currentPassword: currentPwd,
        newPassword: newPwd
      });
      setMessage(t('account.passwordUpdated'));
      setCurrentPwd('');
      setNewPwd('');
      setNewPwd2('');
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <View className="gap-3">
      <Text className="text-base font-semibold text-accanto-900">
        {t('account.changePassword')}
      </Text>
      <TextField
        label={t('account.currentPassword')}
        value={currentPwd}
        onChangeText={setCurrentPwd}
        secureTextEntry
        autoComplete="current-password"
        textContentType="password"
      />
      <TextField
        label={t('account.newPassword')}
        value={newPwd}
        onChangeText={setNewPwd}
        secureTextEntry
        autoComplete="new-password"
        textContentType="newPassword"
        hint={t('account.passwordHint') as string}
      />
      <TextField
        label={t('account.confirmNewPassword')}
        value={newPwd2}
        onChangeText={setNewPwd2}
        secureTextEntry
        autoComplete="new-password"
        textContentType="newPassword"
      />
      <ErrorBanner message={error} />
      {message ? (
        <Text className="text-sm text-green-700">{message}</Text>
      ) : null}
      <Button onPress={submit} busy={busy} disabled={busy}>
        {busy ? t('common.saving') : t('account.updatePassword')}
      </Button>
    </View>
  );
}
