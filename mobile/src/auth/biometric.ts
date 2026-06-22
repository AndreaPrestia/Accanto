import * as LocalAuthentication from 'expo-local-authentication';
import {
  getSecret,
  setSecret,
  clearSecret
} from '../storage/secureStorage';

const BIOMETRIC_ENABLED_KEY = 'accanto.biometric.enabled';

export type BiometricSupport =
  | { available: false; reason: 'no-hardware' | 'not-enrolled' | 'unknown' }
  | {
      available: true;
      types: LocalAuthentication.AuthenticationType[];
    };

export async function checkBiometricSupport(): Promise<BiometricSupport> {
  try {
    const hasHw = await LocalAuthentication.hasHardwareAsync();
    if (!hasHw) return { available: false, reason: 'no-hardware' };
    const enrolled = await LocalAuthentication.isEnrolledAsync();
    if (!enrolled) return { available: false, reason: 'not-enrolled' };
    const types = await LocalAuthentication.supportedAuthenticationTypesAsync();
    return { available: true, types };
  } catch {
    return { available: false, reason: 'unknown' };
  }
}

export async function isBiometricEnabled(): Promise<boolean> {
  const v = await getSecret(BIOMETRIC_ENABLED_KEY);
  return v === '1';
}

export async function setBiometricEnabled(enabled: boolean): Promise<void> {
  if (enabled) {
    await setSecret(BIOMETRIC_ENABLED_KEY, '1');
  } else {
    await clearSecret(BIOMETRIC_ENABLED_KEY);
  }
}

export interface BiometricPromptOptions {
  promptMessage: string;
  cancelLabel?: string;
  disableDeviceFallback?: boolean;
}

export async function authenticateBiometric(
  opts: BiometricPromptOptions
): Promise<{ success: boolean; error?: string }> {
  const res = await LocalAuthentication.authenticateAsync({
    promptMessage: opts.promptMessage,
    cancelLabel: opts.cancelLabel,
    disableDeviceFallback: opts.disableDeviceFallback ?? false
  });
  if (res.success) return { success: true };
  return { success: false, error: res.error ?? 'biometric-failed' };
}
