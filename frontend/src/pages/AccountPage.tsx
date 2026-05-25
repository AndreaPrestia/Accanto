import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import PushNotificationsSection from '../components/PushNotificationsSection';
import NotificationPreferencesSection from '../components/NotificationPreferencesSection';
import LanguageSwitcher from '../components/LanguageSwitcher';
import ActiveSessionsSection from '../components/ActiveSessionsSection';
import TwoFactorSection from '../components/TwoFactorSection';
import SecurityAuditSection from '../components/SecurityAuditSection';

export default function AccountPage() {
  const { user, logout } = useAuth();
  const { t } = useTranslation();
  const navigate = useNavigate();

  // Cambio password
  const [currentPwd, setCurrentPwd] = useState('');
  const [newPwd, setNewPwd] = useState('');
  const [newPwd2, setNewPwd2] = useState('');
  const [pwdMsg, setPwdMsg] = useState<string | null>(null);
  const [pwdError, setPwdError] = useState<string | null>(null);
  const [pwdSubmitting, setPwdSubmitting] = useState(false);

  // Eliminazione account
  const [deletePwd, setDeletePwd] = useState('');
  const [understood, setUnderstood] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [deleteSubmitting, setDeleteSubmitting] = useState(false);

  // Esportazione dati
  const [exportError, setExportError] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);

  if (!user) return null;

  async function submitPassword(e: React.FormEvent) {
    e.preventDefault();
    setPwdMsg(null);
    setPwdError(null);

    if (newPwd !== newPwd2) {
      setPwdError(t('account.passwordsDoNotMatch'));
      return;
    }

    setPwdSubmitting(true);
    try {
      await api.post('/account/change-password', {
        currentPassword: currentPwd,
        newPassword: newPwd
      });
      setPwdMsg(t('account.passwordUpdated'));
      setCurrentPwd('');
      setNewPwd('');
      setNewPwd2('');
    } catch (e) {
      setPwdError(extractError(e));
    } finally {
      setPwdSubmitting(false);
    }
  }

  async function submitDelete(e: React.FormEvent) {
    e.preventDefault();
    setDeleteError(null);

    if (!understood) {
      setDeleteError(t('account.deleteUnderstand'));
      return;
    }

    setDeleteSubmitting(true);
    try {
      await api.delete('/account', { data: { currentPassword: deletePwd } });
      logout();
      navigate('/login', { replace: true });
    } catch (e) {
      setDeleteError(extractError(e));
    } finally {
      setDeleteSubmitting(false);
    }
  }

  async function downloadExport() {
    setExportError(null);
    setExporting(true);
    try {
      const res = await api.get('/account/export', { responseType: 'blob' });
      const disposition: string | undefined = res.headers['content-disposition'];
      let fileName = 'accanto-export.zip';
      if (disposition) {
        const match = /filename\*?=(?:UTF-8'')?["']?([^"';]+)/i.exec(disposition);
        if (match && match[1]) fileName = decodeURIComponent(match[1]);
      }
      const url = URL.createObjectURL(res.data as Blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch (e) {
      setExportError(extractError(e));
    } finally {
      setExporting(false);
    }
  }

  return (
    <div className="space-y-8">
      <header>
        <h1 className="text-xl font-semibold text-accanto-900">{t('account.title')}</h1>
        <p className="text-sm text-accanto-500 mt-1">{user.email}</p>
      </header>

      <section className="space-y-3">
        <h2 className="text-base font-semibold text-accanto-900">{t('account.languageSectionTitle')}</h2>
        <p className="text-sm text-accanto-500">{t('account.languageSectionHint')}</p>
        <LanguageSwitcher />
      </section>

      <section className="space-y-3">
        <h2 className="text-base font-semibold text-accanto-900">{t('account.changePassword')}</h2>
        <form onSubmit={submitPassword} className="space-y-3">
          <div>
            <label className="block text-sm text-accanto-700 mb-1">{t('account.currentPassword')}</label>
            <input
              type="password"
              value={currentPwd}
              onChange={(e) => setCurrentPwd(e.target.value)}
              required
              autoComplete="current-password"
              className="w-full border border-accanto-200 rounded-lg px-3 py-2"
            />
          </div>
          <div>
            <label className="block text-sm text-accanto-700 mb-1">{t('account.newPassword')}</label>
            <input
              type="password"
              value={newPwd}
              onChange={(e) => setNewPwd(e.target.value)}
              required
              minLength={8}
              autoComplete="new-password"
              className="w-full border border-accanto-200 rounded-lg px-3 py-2"
            />
            <p className="text-xs text-accanto-500 mt-1">{t('account.passwordHint')}</p>
          </div>
          <div>
            <label className="block text-sm text-accanto-700 mb-1">{t('account.confirmNewPassword')}</label>
            <input
              type="password"
              value={newPwd2}
              onChange={(e) => setNewPwd2(e.target.value)}
              required
              minLength={8}
              autoComplete="new-password"
              className="w-full border border-accanto-200 rounded-lg px-3 py-2"
            />
          </div>
          {pwdError && <p className="text-sm text-red-700">{pwdError}</p>}
          {pwdMsg && <p className="text-sm text-green-700">{pwdMsg}</p>}
          <button
            type="submit"
            disabled={pwdSubmitting}
            className="w-full sm:w-auto px-4 py-2 rounded-lg bg-accanto-700 text-white disabled:opacity-60"
          >
            {pwdSubmitting ? t('common.saving') : t('account.updatePassword')}
          </button>
        </form>
      </section>

      <PushNotificationsSection />

      <NotificationPreferencesSection />

      <ActiveSessionsSection />

      <TwoFactorSection />

      <SecurityAuditSection />

      <section className="space-y-3">
        <h2 className="text-base font-semibold text-accanto-900">{t('account.exportTitle')}</h2>
        <p className="text-sm text-accanto-500">{t('account.exportHint')}</p>
        {exportError && <p className="text-sm text-red-600">{exportError}</p>}
        <button
          type="button"
          onClick={downloadExport}
          disabled={exporting}
          className="rounded-lg bg-accanto-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
        >
          {exporting ? t('account.exportPreparing') : t('account.exportCta')}
        </button>
      </section>

      <section className="space-y-3 border-t border-accanto-100 pt-6">
        <h2 className="text-base font-semibold text-red-800">{t('account.deleteTitle')}</h2>
        <p className="text-sm text-accanto-700">
          {t('account.deleteDescription')}
        </p>
        <form onSubmit={submitDelete} className="space-y-3">
          <div>
            <label className="block text-sm text-accanto-700 mb-1">{t('account.deleteConfirmLabel')}</label>
            <input
              type="password"
              value={deletePwd}
              onChange={(e) => setDeletePwd(e.target.value)}
              required
              autoComplete="current-password"
              className="w-full border border-accanto-200 rounded-lg px-3 py-2"
            />
          </div>
          <label className="flex items-start gap-2 text-sm text-accanto-700">
            <input
              type="checkbox"
              checked={understood}
              onChange={(e) => setUnderstood(e.target.checked)}
              className="mt-1"
            />
            <span>{t('account.deleteUnderstand')}</span>
          </label>
          {deleteError && <p className="text-sm text-red-700">{deleteError}</p>}
          <button
            type="submit"
            disabled={deleteSubmitting || !understood}
            className="w-full sm:w-auto px-4 py-2 rounded-lg bg-red-700 text-white disabled:opacity-60"
          >
            {deleteSubmitting ? t('account.deleting') : t('account.deleteCta')}
          </button>
        </form>
      </section>
    </div>
  );
}
