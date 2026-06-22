import { useEffect, useState } from 'react';
import {
  Alert,
  Pressable,
  Text,
  View
} from 'react-native';
import * as Clipboard from 'expo-clipboard';
import QRCode from 'react-native-qrcode-svg';
import { useTranslation } from 'react-i18next';
import Button from '../ui/Button';
import TextField from '../ui/TextField';
import ErrorBanner from '../ui/ErrorBanner';
import { api, extractError } from '../../api/client';
import type {
  TwoFactorEnableResponse,
  TwoFactorSetupResponse,
  TwoFactorStatus
} from '@accanto/shared/types';

export default function TwoFactorSection() {
  const { t } = useTranslation();
  const [status, setStatus] = useState<TwoFactorStatus | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [setup, setSetup] = useState<TwoFactorSetupResponse | null>(null);
  const [setupBusy, setSetupBusy] = useState(false);
  const [setupError, setSetupError] = useState<string | null>(null);

  const [enableCode, setEnableCode] = useState('');
  const [enableBusy, setEnableBusy] = useState(false);
  const [enableError, setEnableError] = useState<string | null>(null);
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);

  const [disablePwd, setDisablePwd] = useState('');
  const [disableCode, setDisableCode] = useState('');
  const [disableBusy, setDisableBusy] = useState(false);
  const [disableError, setDisableError] = useState<string | null>(null);

  const [regenPwd, setRegenPwd] = useState('');
  const [regenBusy, setRegenBusy] = useState(false);
  const [regenError, setRegenError] = useState<string | null>(null);

  const refresh = async () => {
    try {
      const { data } = await api.get<TwoFactorStatus>('/account/2fa');
      setStatus(data);
    } catch (e) {
      setLoadError(extractError(e));
    }
  };

  useEffect(() => {
    refresh();
  }, []);

  const startSetup = async () => {
    setSetupError(null);
    setSetupBusy(true);
    try {
      const { data } = await api.post<TwoFactorSetupResponse>(
        '/account/2fa/setup'
      );
      setSetup(data);
      setEnableCode('');
      setRecoveryCodes(null);
    } catch (e) {
      setSetupError(extractError(e));
    } finally {
      setSetupBusy(false);
    }
  };

  const submitEnable = async () => {
    setEnableError(null);
    setEnableBusy(true);
    try {
      const { data } = await api.post<TwoFactorEnableResponse>(
        '/account/2fa/enable',
        { code: enableCode }
      );
      setRecoveryCodes(data.recoveryCodes);
      setSetup(null);
      await refresh();
    } catch (e) {
      setEnableError(extractError(e));
    } finally {
      setEnableBusy(false);
    }
  };

  const submitDisable = async () => {
    setDisableError(null);
    setDisableBusy(true);
    try {
      await api.post('/account/2fa/disable', {
        password: disablePwd,
        code: disableCode || null,
        recoveryCode: null
      });
      setDisablePwd('');
      setDisableCode('');
      setRecoveryCodes(null);
      await refresh();
    } catch (e) {
      setDisableError(extractError(e));
    } finally {
      setDisableBusy(false);
    }
  };

  const submitRegen = async () => {
    setRegenError(null);
    setRegenBusy(true);
    try {
      const { data } = await api.post<TwoFactorEnableResponse>(
        '/account/2fa/recovery-codes',
        { password: regenPwd }
      );
      setRecoveryCodes(data.recoveryCodes);
      setRegenPwd('');
      await refresh();
    } catch (e) {
      setRegenError(extractError(e));
    } finally {
      setRegenBusy(false);
    }
  };

  const copySecret = async (s: string) => {
    await Clipboard.setStringAsync(s);
    Alert.alert('Copiato', 'Segreto copiato negli appunti.');
  };

  const copyRecoveryCodes = async () => {
    if (!recoveryCodes) return;
    await Clipboard.setStringAsync(recoveryCodes.join('\n'));
    Alert.alert('Copiati', 'I codici di recupero sono negli appunti.');
  };

  return (
    <View className="gap-3">
      <Text className="text-base font-semibold text-accanto-900">
        {t('account.twoFactorTitle')}
      </Text>
      <Text className="text-sm text-accanto-500">
        {t('account.twoFactorHint')}
      </Text>
      {loadError ? <ErrorBanner message={loadError} /> : null}

      {recoveryCodes ? (
        <View className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 gap-2">
          <Text className="text-sm font-medium text-amber-900">
            {t('account.twoFactorRecoveryHeading')}
          </Text>
          <Text className="text-xs text-amber-800">
            {t('account.twoFactorRecoveryHint')}
          </Text>
          <View className="flex-row flex-wrap gap-x-4 gap-y-1">
            {recoveryCodes.map((c) => (
              <Text
                key={c}
                className="font-mono text-sm text-amber-900"
                style={{ fontFamily: 'monospace' }}
              >
                {c}
              </Text>
            ))}
          </View>
          <Pressable
            onPress={copyRecoveryCodes}
            className="self-start mt-1 px-3 py-1.5 rounded border border-amber-300"
          >
            <Text className="text-xs text-amber-900">Copia tutti</Text>
          </Pressable>
        </View>
      ) : null}

      {status && !status.enabled && !setup ? (
        <View className="gap-2">
          <Text className="text-sm text-accanto-700">
            {t('account.twoFactorDisabled')}
          </Text>
          <ErrorBanner message={setupError} />
          <Button onPress={startSetup} busy={setupBusy} disabled={setupBusy}>
            {setupBusy ? t('common.loading') : t('account.twoFactorEnableCta')}
          </Button>
        </View>
      ) : null}

      {setup ? (
        <View className="border border-accanto-100 rounded-lg p-4 gap-3 bg-white">
          <Text className="text-sm text-accanto-700">
            {t('account.twoFactorScanHint')}
          </Text>
          <View className="items-center py-2">
            <QRCode value={setup.otpAuthUri} size={220} />
          </View>
          <View>
            <Text className="text-xs text-accanto-500">
              {t('account.twoFactorSecretLabel')}
            </Text>
            <Pressable onPress={() => copySecret(setup.secret)}>
              <Text
                className="text-xs text-accanto-900"
                style={{ fontFamily: 'monospace' }}
                numberOfLines={1}
              >
                {setup.secret}
              </Text>
            </Pressable>
          </View>
          <TextField
            label={t('account.twoFactorCodeLabel')}
            value={enableCode}
            onChangeText={setEnableCode}
            keyboardType="number-pad"
            autoComplete="one-time-code"
            textContentType="oneTimeCode"
            maxLength={8}
          />
          <ErrorBanner message={enableError} />
          <View className="flex-row gap-2">
            <View className="flex-1">
              <Button
                onPress={submitEnable}
                busy={enableBusy}
                disabled={enableBusy}
              >
                {enableBusy
                  ? t('common.saving')
                  : t('account.twoFactorConfirmCta')}
              </Button>
            </View>
            <View className="flex-1">
              <Button variant="ghost" onPress={() => setSetup(null)}>
                {t('common.cancel')}
              </Button>
            </View>
          </View>
        </View>
      ) : null}

      {status?.enabled ? (
        <View className="gap-3">
          <Text className="text-sm text-accanto-700">
            {t('account.twoFactorEnabled')} \u00b7{' '}
            {t('account.twoFactorRecoveryRemaining', {
              count: status.remainingRecoveryCodes
            })}
          </Text>

          <View className="border border-accanto-100 rounded-lg p-4 gap-3 bg-white">
            <Text className="text-sm font-semibold text-accanto-900">
              {t('account.twoFactorDisableTitle')}
            </Text>
            <TextField
              label={t('account.currentPassword')}
              value={disablePwd}
              onChangeText={setDisablePwd}
              secureTextEntry
              autoComplete="current-password"
              textContentType="password"
            />
            <TextField
              label={t('account.twoFactorCodeLabel')}
              value={disableCode}
              onChangeText={setDisableCode}
              keyboardType="number-pad"
              maxLength={8}
            />
            <ErrorBanner message={disableError} />
            <Button
              onPress={submitDisable}
              busy={disableBusy}
              disabled={disableBusy}
              variant="danger"
            >
              {disableBusy
                ? t('common.saving')
                : t('account.twoFactorDisableCta')}
            </Button>
          </View>

          <View className="border border-accanto-100 rounded-lg p-4 gap-3 bg-white">
            <Text className="text-sm font-semibold text-accanto-900">
              {t('account.twoFactorRegenTitle')}
            </Text>
            <TextField
              label={t('account.currentPassword')}
              value={regenPwd}
              onChangeText={setRegenPwd}
              secureTextEntry
              autoComplete="current-password"
              textContentType="password"
            />
            <ErrorBanner message={regenError} />
            <Button onPress={submitRegen} busy={regenBusy} disabled={regenBusy}>
              {regenBusy
                ? t('common.saving')
                : t('account.twoFactorRegenCta')}
            </Button>
          </View>
        </View>
      ) : null}
    </View>
  );
}
