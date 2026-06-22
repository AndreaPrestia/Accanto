import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import TextField from '../components/ui/TextField';
import ErrorBanner from '../components/ui/ErrorBanner';
import { useAuth } from '../auth/AuthContext';
import { extractError } from '../api/client';
import type { AuthScreenProps } from '../navigation/types';

type Props = AuthScreenProps<'Register'>;

export default function RegisterScreen({ navigation }: Props) {
  const { register } = useAuth();
  const { t } = useTranslation();

  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setError(null);
    setBusy(true);
    try {
      await register({
        email: email.trim(),
        displayName: displayName.trim(),
        password
      });
      // RootNavigator switcha automaticamente sull'AppStack.
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen>
      <View className="max-w-md w-full self-center pt-2">
        <Text className="text-2xl font-semibold text-accanto-900 mb-2">
          {t('auth.registerTitle')}
        </Text>
        <Text className="text-accanto-500 mb-6">{t('auth.registerSubtitle')}</Text>

        <View className="gap-3">
          <TextField
            label={t('auth.yourName')}
            value={displayName}
            onChangeText={setDisplayName}
            autoCapitalize="words"
            autoComplete="name"
            textContentType="name"
          />
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
          <TextField
            label={t('auth.password')}
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            autoCapitalize="none"
            autoComplete="password-new"
            textContentType="newPassword"
            hint={t('auth.passwordHint')}
          />

          <ErrorBanner message={error} />

          <View className="mt-2">
            <Button onPress={submit} busy={busy} disabled={busy}>
              {busy ? t('auth.creating') : t('auth.registerCta')}
            </Button>
          </View>
        </View>

        <View className="mt-6 flex-row flex-wrap">
          <Text className="text-sm text-accanto-500">{t('auth.hasAccount')} </Text>
          <Pressable onPress={() => navigation.navigate('Login')}>
            <Text className="text-sm text-accanto-700 underline">
              {t('auth.signIn')}
            </Text>
          </Pressable>
        </View>
      </View>
    </Screen>
  );
}
