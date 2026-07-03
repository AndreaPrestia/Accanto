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

type Props = AuthScreenProps<'Login'>;

export default function LoginScreen({ navigation }: Props) {
  const { login, completeTwoFactor } = useAuth();
  const { t } = useTranslation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Step 2FA
  const [twoFactorToken, setTwoFactorToken] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [useRecovery, setUseRecovery] = useState(false);
  const [recoveryCode, setRecoveryCode] = useState('');

  const submit = async () => {
    setError(null);
    setBusy(true);
    try {
      const result = await login({ email: email.trim(), password });
      if (result.requiresTwoFactor && result.twoFactorToken) {
        setTwoFactorToken(result.twoFactorToken);
      }
      // Se non serve 2FA, RootNavigator switcha automaticamente su AppStack.
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  const submitTwoFactor = async () => {
    if (!twoFactorToken) return;
    setError(null);
    setBusy(true);
    try {
      await completeTwoFactor(
        twoFactorToken,
        useRecovery ? undefined : code.replace(/\s+/g, ''),
        useRecovery ? recoveryCode.trim() : undefined
      );
      // RootNavigator switcha su AppStack quando `user` è popolato.
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  if (twoFactorToken) {
    return (
      <Screen edges={['top', 'bottom', 'left', 'right']}>
        <View className="max-w-md w-full self-center pt-6">
          <Text className="text-2xl font-semibold text-accanto-900 mb-2">
            {t('auth.twoFactorTitle')}
          </Text>
          <Text className="text-accanto-500 mb-6">{t('auth.twoFactorSubtitle')}</Text>

          {!useRecovery ? (
            <TextField
              label={t('auth.twoFactorCode')}
              value={code}
              onChangeText={setCode}
              keyboardType="number-pad"
              autoComplete="one-time-code"
              textContentType="oneTimeCode"
              maxLength={8}
              autoFocus
              testID="login-2fa-code"
            />
          ) : (
            <TextField
              label={t('auth.twoFactorRecoveryCode')}
              value={recoveryCode}
              onChangeText={setRecoveryCode}
              autoCapitalize="characters"
              autoCorrect={false}
              autoComplete="off"
              autoFocus
              testID="login-2fa-recovery"
            />
          )}

          <View className="mt-3 mb-4">
            <ErrorBanner message={error} />
          </View>

          <Button onPress={submitTwoFactor} busy={busy} disabled={busy}>
            {busy ? t('auth.loggingIn') : t('auth.twoFactorVerify')}
          </Button>

          <Pressable
            className="mt-4 py-2"
            onPress={() => {
              setUseRecovery(!useRecovery);
              setError(null);
              setCode('');
              setRecoveryCode('');
            }}
          >
            <Text className="text-sm text-accanto-700 underline">
              {useRecovery
                ? t('auth.twoFactorUseCode')
                : t('auth.twoFactorUseRecovery')}
            </Text>
          </Pressable>
        </View>
      </Screen>
    );
  }

  return (
    <Screen edges={['top', 'bottom', 'left', 'right']}>
      <View className="max-w-md w-full self-center pt-6">
        <Text className="text-2xl font-semibold text-accanto-900 mb-2">
          {t('auth.loginTitle')}
        </Text>
        <Text className="text-accanto-500 mb-6">{t('auth.loginSubtitle')}</Text>

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
            testID="login-email"
          />
          <TextField
            label={t('auth.password')}
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            autoCapitalize="none"
            autoComplete="password"
            textContentType="password"
            testID="login-password"
          />

          <ErrorBanner message={error} />

          <View className="mt-2">
            <Button onPress={submit} busy={busy} disabled={busy} testID="login-submit">
              {busy ? t('auth.loggingIn') : t('auth.loginCta')}
            </Button>
          </View>
        </View>

        <Pressable
          className="mt-4 py-2"
          onPress={() => navigation.navigate('ForgotPassword')}
        >
          <Text className="text-sm text-accanto-700 underline">
            {t('auth.forgotPassword')}
          </Text>
        </Pressable>

        <View className="mt-6 flex-row flex-wrap">
          <Text className="text-sm text-accanto-500">
            {t('auth.noAccount')}{' '}
          </Text>
          <Pressable onPress={() => navigation.navigate('Register')}>
            <Text className="text-sm text-accanto-700 underline">
              {t('auth.createSpace')}
            </Text>
          </Pressable>
        </View>
      </View>
    </Screen>
  );
}
