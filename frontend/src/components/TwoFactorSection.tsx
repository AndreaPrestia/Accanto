import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import QRCode from 'qrcode';
import { api, extractError } from '../api/client';
import {
  TwoFactorEnableResponse,
  TwoFactorSetupResponse,
  TwoFactorStatus
} from '../types';

export default function TwoFactorSection() {
  const { t } = useTranslation();

  const [status, setStatus] = useState<TwoFactorStatus | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  // Setup
  const [setup, setSetup] = useState<TwoFactorSetupResponse | null>(null);
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null);
  const [setupBusy, setSetupBusy] = useState(false);
  const [setupError, setSetupError] = useState<string | null>(null);

  // Enable
  const [enableCode, setEnableCode] = useState('');
  const [enableBusy, setEnableBusy] = useState(false);
  const [enableError, setEnableError] = useState<string | null>(null);
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);

  // Disable
  const [disablePwd, setDisablePwd] = useState('');
  const [disableCode, setDisableCode] = useState('');
  const [disableBusy, setDisableBusy] = useState(false);
  const [disableError, setDisableError] = useState<string | null>(null);

  // Regenerate
  const [regenPwd, setRegenPwd] = useState('');
  const [regenBusy, setRegenBusy] = useState(false);
  const [regenError, setRegenError] = useState<string | null>(null);

  async function refresh() {
    try {
      const { data } = await api.get<TwoFactorStatus>('/account/2fa');
      setStatus(data);
    } catch (e) {
      setLoadError(extractError(e));
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  useEffect(() => {
    if (setup?.otpAuthUri) {
      QRCode.toDataURL(setup.otpAuthUri, { width: 220, margin: 1 })
        .then(setQrDataUrl)
        .catch(() => setQrDataUrl(null));
    } else {
      setQrDataUrl(null);
    }
  }, [setup]);

  async function startSetup() {
    setSetupError(null);
    setSetupBusy(true);
    try {
      const { data } = await api.post<TwoFactorSetupResponse>('/account/2fa/setup');
      setSetup(data);
      setEnableCode('');
      setRecoveryCodes(null);
    } catch (e) {
      setSetupError(extractError(e));
    } finally {
      setSetupBusy(false);
    }
  }

  async function submitEnable(e: React.FormEvent) {
    e.preventDefault();
    setEnableError(null);
    setEnableBusy(true);
    try {
      const { data } = await api.post<TwoFactorEnableResponse>('/account/2fa/enable', { code: enableCode });
      setRecoveryCodes(data.recoveryCodes);
      setSetup(null);
      await refresh();
    } catch (e) {
      setEnableError(extractError(e));
    } finally {
      setEnableBusy(false);
    }
  }

  async function submitDisable(e: React.FormEvent) {
    e.preventDefault();
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
  }

  async function submitRegen(e: React.FormEvent) {
    e.preventDefault();
    setRegenError(null);
    setRegenBusy(true);
    try {
      const { data } = await api.post<TwoFactorEnableResponse>('/account/2fa/recovery-codes', { password: regenPwd });
      setRecoveryCodes(data.recoveryCodes);
      setRegenPwd('');
      await refresh();
    } catch (e) {
      setRegenError(extractError(e));
    } finally {
      setRegenBusy(false);
    }
  }

  return (
    <section className="space-y-3">
      <h2 className="text-base font-semibold text-accanto-900">{t('account.twoFactorTitle')}</h2>
      <p className="text-sm text-accanto-500">{t('account.twoFactorHint')}</p>
      {loadError && <p className="text-sm text-red-700">{loadError}</p>}

      {recoveryCodes && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 space-y-2">
          <p className="text-sm font-medium text-amber-900">{t('account.twoFactorRecoveryHeading')}</p>
          <p className="text-xs text-amber-800">{t('account.twoFactorRecoveryHint')}</p>
          <ul className="grid grid-cols-2 gap-x-4 gap-y-1 font-mono text-sm text-amber-900">
            {recoveryCodes.map((c) => (<li key={c}>{c}</li>))}
          </ul>
        </div>
      )}

      {status && !status.enabled && !setup && (
        <div className="space-y-2">
          <p className="text-sm text-accanto-700">{t('account.twoFactorDisabled')}</p>
          {setupError && <p className="text-sm text-red-700">{setupError}</p>}
          <button
            type="button"
            onClick={startSetup}
            disabled={setupBusy}
            className="px-4 py-2 rounded-lg bg-accanto-700 text-white disabled:opacity-60"
          >
            {setupBusy ? t('common.loading') : t('account.twoFactorEnableCta')}
          </button>
        </div>
      )}

      {setup && (
        <form onSubmit={submitEnable} className="space-y-3 border border-accanto-200 rounded-lg p-4">
          <p className="text-sm text-accanto-700">{t('account.twoFactorScanHint')}</p>
          {qrDataUrl && <img src={qrDataUrl} alt="QR" className="w-56 h-56" />}
          <div>
            <p className="text-xs text-accanto-500">{t('account.twoFactorSecretLabel')}</p>
            <code className="text-xs break-all">{setup.secret}</code>
          </div>
          <div>
            <label className="block text-sm text-accanto-700 mb-1">{t('account.twoFactorCodeLabel')}</label>
            <input
              type="text"
              inputMode="numeric"
              autoComplete="one-time-code"
              pattern="[0-9 ]*"
              maxLength={8}
              value={enableCode}
              onChange={(e) => setEnableCode(e.target.value)}
              required
              className="w-full border border-accanto-200 rounded-lg px-3 py-2"
            />
          </div>
          {enableError && <p className="text-sm text-red-700">{enableError}</p>}
          <div className="flex gap-2">
            <button
              type="submit"
              disabled={enableBusy}
              className="px-4 py-2 rounded-lg bg-accanto-700 text-white disabled:opacity-60"
            >
              {enableBusy ? t('common.saving') : t('account.twoFactorConfirmCta')}
            </button>
            <button
              type="button"
              onClick={() => setSetup(null)}
              className="px-4 py-2 rounded-lg border border-accanto-200"
            >
              {t('common.cancel')}
            </button>
          </div>
        </form>
      )}

      {status?.enabled && (
        <>
          <p className="text-sm text-accanto-700">
            {t('account.twoFactorEnabled')} ·{' '}
            {t('account.twoFactorRecoveryRemaining', { count: status.remainingRecoveryCodes })}
          </p>

          <form onSubmit={submitDisable} className="space-y-3 border border-accanto-200 rounded-lg p-4">
            <h3 className="text-sm font-semibold text-accanto-900">{t('account.twoFactorDisableTitle')}</h3>
            <div>
              <label className="block text-sm text-accanto-700 mb-1">{t('account.currentPassword')}</label>
              <input
                type="password"
                value={disablePwd}
                onChange={(e) => setDisablePwd(e.target.value)}
                required
                autoComplete="current-password"
                className="w-full border border-accanto-200 rounded-lg px-3 py-2"
              />
            </div>
            <div>
              <label className="block text-sm text-accanto-700 mb-1">{t('account.twoFactorCodeLabel')}</label>
              <input
                type="text"
                inputMode="numeric"
                pattern="[0-9 ]*"
                maxLength={8}
                value={disableCode}
                onChange={(e) => setDisableCode(e.target.value)}
                required
                className="w-full border border-accanto-200 rounded-lg px-3 py-2"
              />
            </div>
            {disableError && <p className="text-sm text-red-700">{disableError}</p>}
            <button
              type="submit"
              disabled={disableBusy}
              className="px-4 py-2 rounded-lg bg-red-700 text-white disabled:opacity-60"
            >
              {disableBusy ? t('common.saving') : t('account.twoFactorDisableCta')}
            </button>
          </form>

          <form onSubmit={submitRegen} className="space-y-3 border border-accanto-200 rounded-lg p-4">
            <h3 className="text-sm font-semibold text-accanto-900">{t('account.twoFactorRegenTitle')}</h3>
            <div>
              <label className="block text-sm text-accanto-700 mb-1">{t('account.currentPassword')}</label>
              <input
                type="password"
                value={regenPwd}
                onChange={(e) => setRegenPwd(e.target.value)}
                required
                autoComplete="current-password"
                className="w-full border border-accanto-200 rounded-lg px-3 py-2"
              />
            </div>
            {regenError && <p className="text-sm text-red-700">{regenError}</p>}
            <button
              type="submit"
              disabled={regenBusy}
              className="px-4 py-2 rounded-lg bg-accanto-700 text-white disabled:opacity-60"
            >
              {regenBusy ? t('common.saving') : t('account.twoFactorRegenCta')}
            </button>
          </form>
        </>
      )}
    </section>
  );
}
