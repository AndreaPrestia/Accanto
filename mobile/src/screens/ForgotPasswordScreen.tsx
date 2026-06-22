import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import TextField from '../components/ui/TextField';
import ErrorBanner from '../components/ui/ErrorBanner';
import { api, extractError } from '../api/client';
import type { AuthScreenProps } from '../navigation/types';

type Props = AuthScreenProps<'ForgotPassword'>;

export default function ForgotPasswordScreen({ navigation }: Props) {
  const { t } = useTranslation();
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sent, setSent] = useState(false);

  const submit = async () => {
    setError(null);
    setBusy(true);
    try {
      await api.post('/auth/forgot-password', { email: email.trim() });
      setSent(true);
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  if (sent) {
    return (
      <Screen>
        <View className="max-w-md w-full self-center pt-2">
          <Text className="text-2xl font-semibold text-accanto-900 mb-3">
            {t('auth.forgotPasswordTitle')}
          </Text>
          <View className="rounded-md border border-accanto-200 bg-accanto-50 px-3 py-3">
            <Text className="text-accanto-700">{t('auth.forgotPasswordSent')}</Text>
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

  return (
    <Screen>
      <View className="max-w-md w-full self-center pt-2">
        <Text className="text-2xl font-semibold text-accanto-900 mb-2">
          {t('auth.forgotPasswordTitle')}
        </Text>
        <Text className="text-accanto-500 mb-6">
          {t('auth.forgotPasswordSubtitle')}
        </Text>

        <View className="gap-3">
          <TextField
            label={t('auth.email')}
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
            autoCorrect={false}
            autoComplete="email"
            textContentType="emailAddress"
          />

          <ErrorBanner message={error} />

          <View className="mt-2">
            <Button onPress={submit} busy={busy} disabled={busy}>
              {busy ? t('common.saving') : t('auth.forgotPasswordCta')}
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
