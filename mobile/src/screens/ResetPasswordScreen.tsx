import { useEffect, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import TextField from '../components/ui/TextField';
import ErrorBanner from '../components/ui/ErrorBanner';
import { api, extractError } from '../api/client';
import type { AuthScreenProps } from '../navigation/types';

type Props = AuthScreenProps<'ResetPassword'>;

export default function ResetPasswordScreen({ navigation, route }: Props) {
  const { t } = useTranslation();
  const token = route.params?.token ?? '';

  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  // Dopo il successo, dopo ~2.5s torniamo al Login.
  useEffect(() => {
    if (!done) return;
    const id = setTimeout(() => {
      navigation.reset({ index: 0, routes: [{ name: 'Login' }] });
    }, 2500);
    return () => clearTimeout(id);
  }, [done, navigation]);

  const submit = async () => {
    if (!token) return;
    if (password.length < 8) {
      setError(t('auth.passwordHint'));
      return;
    }
    if (password !== confirm) {
      setError(t('auth.passwordsDoNotMatch'));
      return;
    }
    setError(null);
    setBusy(true);
    try {
      await api.post('/auth/reset-password', { token, newPassword: password });
      setDone(true);
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!token) {
    return (
      <Screen>
        <View className="max-w-md w-full self-center pt-2">
          <Text className="text-2xl font-semibold text-accanto-900 mb-3">
            {t('auth.resetPasswordTitle')}
          </Text>
          <ErrorBanner message={t('auth.resetPasswordTokenInvalid')} />
          <Pressable
            className="mt-6 py-2"
            onPress={() => navigation.navigate('ForgotPassword')}
          >
            <Text className="text-sm text-accanto-700 underline">
              {t('auth.forgotPasswordCta')}
            </Text>
          </Pressable>
        </View>
      </Screen>
    );
  }

  if (done) {
    return (
      <Screen>
        <View className="max-w-md w-full self-center pt-2">
          <Text className="text-2xl font-semibold text-accanto-900 mb-3">
            {t('auth.resetPasswordTitle')}
          </Text>
          <View className="rounded-md border border-accanto-200 bg-accanto-50 px-3 py-3">
            <Text className="text-accanto-700">{t('auth.resetPasswordSuccess')}</Text>
          </View>
        </View>
      </Screen>
    );
  }

  return (
    <Screen>
      <View className="max-w-md w-full self-center pt-2">
        <Text className="text-2xl font-semibold text-accanto-900 mb-2">
          {t('auth.resetPasswordTitle')}
        </Text>
        <Text className="text-accanto-500 mb-6">
          {t('auth.resetPasswordSubtitle')}
        </Text>

        <View className="gap-3">
          <TextField
            label={t('auth.newPassword')}
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            autoCapitalize="none"
            autoComplete="password-new"
            textContentType="newPassword"
            hint={t('auth.passwordHint')}
          />
          <TextField
            label={t('auth.confirmNewPassword')}
            value={confirm}
            onChangeText={setConfirm}
            secureTextEntry
            autoCapitalize="none"
            autoComplete="password-new"
            textContentType="newPassword"
          />

          <ErrorBanner message={error} />

          <View className="mt-2">
            <Button onPress={submit} busy={busy} disabled={busy}>
              {busy ? t('common.saving') : t('auth.resetPasswordCta')}
            </Button>
          </View>
        </View>

        <Pressable
          className="mt-6 py-2"
          onPress={() => navigation.navigate('Login')}
        >
          <Text className="text-sm text-accanto-700 underline">
            {t('auth.backToLogin')}
          </Text>
        </Pressable>
      </View>
    </Screen>
  );
}
