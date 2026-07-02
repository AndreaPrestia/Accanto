import { useEffect, useMemo, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import type { DrawerNavigationProp } from '@react-navigation/drawer';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import AccordionSection from '../components/AccordionSection';
import LanguageSection from '../components/account/LanguageSection';
import ChangePasswordSection from '../components/account/ChangePasswordSection';
import NotificationPreferencesSection from '../components/account/NotificationPreferencesSection';
import PushDevicesSection from '../components/account/PushDevicesSection';
import ActiveSessionsSection from '../components/account/ActiveSessionsSection';
import TwoFactorSection from '../components/account/TwoFactorSection';
import SecurityAuditSection from '../components/account/SecurityAuditSection';
import WellbeingSection from '../components/account/WellbeingSection';
import ExportSection from '../components/account/ExportSection';
import DeleteAccountSection from '../components/account/DeleteAccountSection';
import { useAuth } from '../auth/AuthContext';
import { api } from '../api/client';
import type { TwoFactorStatus } from '@accanto/shared/types';
import type { AppDrawerParamList } from '../navigation/types';

/**
 * Schermata Account: 4 accordion (Profilo aperto di default, Sicurezza, Dati,
 * Benessere) + zona destructive standalone (elimina account). Il gruppo
 * corretto viene espanso automaticamente se il navigator riceve
 * `params.section` (es. dal SecurityBanner della Dashboard).
 */
export default function AccountScreen() {
  const { user, logout } = useAuth();
  const { t } = useTranslation();
  const navigation =
    useNavigation<DrawerNavigationProp<AppDrawerParamList>>();
  const route = useRoute<RouteProp<AppDrawerParamList, 'Account'>>();
  const section = route.params?.section;

  // Hint numerico su "Sicurezza": 1 se 2FA non attiva, 0 altrimenti. Errore
  // silenzioso — il hint è opzionale, non deve rumoreggiare in caso di 401.
  const [securityHints, setSecurityHints] = useState(0);
  useEffect(() => {
    let cancelled = false;
    api
      .get<TwoFactorStatus>('/account/2fa')
      .then((r) => {
        if (!cancelled) setSecurityHints(r.data.enabled ? 0 : 1);
      })
      .catch(() => {
        /* opzionale */
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const securityHint = useMemo(() => {
    if (securityHints <= 0) return null;
    return t('account.hints.security.enable2fa', { count: securityHints });
  }, [securityHints, t]);

  if (!user) return null;

  return (
    <Screen>
      <View className="gap-1 mb-4">
        <Text className="text-xl font-semibold text-accanto-900">
          {t('account.title')}
        </Text>
        <Text className="text-sm text-accanto-500">{user.email}</Text>
      </View>

      <View className="gap-3">
        <AccordionSection title={t('account.groups.profile')} defaultOpen>
          <LanguageSection />
          <ChangePasswordSection />
        </AccordionSection>

        <AccordionSection
          title={t('account.groups.security')}
          hint={securityHint}
          defaultOpen={section === 'security'}
        >
          <PushDevicesSection />
          <NotificationPreferencesSection />
          <ActiveSessionsSection />
          <TwoFactorSection />
          <SecurityAuditSection />
        </AccordionSection>

        <AccordionSection
          title={t('account.groups.data')}
          defaultOpen={section === 'data'}
        >
          <View className="gap-2">
            <Text className="text-base font-semibold text-accanto-900">
              {t('ai.history.title')}
            </Text>
            <Text className="text-sm text-accanto-500">
              {t('ai.history.subtitle')}
            </Text>
            <Pressable onPress={() => navigation.navigate('AiHistory')}>
              <Text className="text-sm text-accanto-700 underline">
                → {t('ai.history.open') as string}
              </Text>
            </Pressable>
          </View>
          <ExportSection />
        </AccordionSection>

        <AccordionSection
          title={t('account.groups.wellbeing')}
          defaultOpen={section === 'wellbeing'}
        >
          <WellbeingSection />
        </AccordionSection>

        <View className="gap-3 mt-4 border-t border-accanto-100 pt-6">
          <Text className="text-base font-semibold text-accanto-900">
            Sessione
          </Text>
          <Pressable
            onPress={() => logout()}
            className="self-start px-4 py-2 rounded-lg border border-accanto-100"
          >
            <Text className="text-sm text-accanto-700">Esci da questo account</Text>
          </Pressable>
        </View>

        <DeleteAccountSection />
      </View>
    </Screen>
  );
}
