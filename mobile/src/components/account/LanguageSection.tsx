import { useMemo, useState } from 'react';
import { Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import SelectField from '../ui/SelectField';
import { useAuth } from '../../auth/AuthContext';
import { api } from '../../api/client';
import {
  SUPPORTED_LANGUAGES,
  LANGUAGE_LABEL,
  type SupportedLanguage
} from '../../i18n';

export default function LanguageSection() {
  const { i18n, t } = useTranslation();
  const { user, setLanguage } = useAuth();
  const [busy, setBusy] = useState(false);

  const supported = SUPPORTED_LANGUAGES as readonly string[];
  const current = supported.includes(i18n.resolvedLanguage ?? '')
    ? (i18n.resolvedLanguage as SupportedLanguage)
    : 'it';

  const options = useMemo(
    () =>
      SUPPORTED_LANGUAGES.map((l) => ({
        value: l,
        label: LANGUAGE_LABEL[l]
      })),
    []
  );

  const change = async (value: string | null) => {
    if (!value || value === current) return;
    const lang = value as SupportedLanguage;
    setLanguage(lang);
    if (user) {
      setBusy(true);
      try {
        await api.put('/account/language', { language: lang });
      } catch {
        // Anche se la persistenza fallisce il cambio in app resta attivo.
      } finally {
        setBusy(false);
      }
    }
  };

  return (
    <View className="gap-2">
      <Text className="text-base font-semibold text-accanto-900">
        {t('account.languageSectionTitle')}
      </Text>
      <Text className="text-sm text-accanto-500">
        {t('account.languageSectionHint')}
      </Text>
      <SelectField
        value={current}
        onChange={change}
        options={options}
        disabled={busy}
      />
    </View>
  );
}
