import { Pressable, Text, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { DrawerNavigationProp } from '@react-navigation/drawer';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
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
import type { AppDrawerParamList } from '../navigation/types';

/**
 * Schermata Account: composizione delle sezioni indipendenti. Ognuna gestisce
 * il proprio stato di caricamento, errori e API. La sezione push web è
 * sostituita da una nota interim — il flusso push mobile arriva in Phase 6.
 */
export default function AccountScreen() {
  const { user, logout } = useAuth();
  const { t } = useTranslation();
  const navigation =
    useNavigation<DrawerNavigationProp<AppDrawerParamList>>();

  if (!user) return null;

  return (
    <Screen>
      <View className="gap-1 mb-6">
        <Text className="text-xl font-semibold text-accanto-900">
          {t('account.title')}
        </Text>
        <Text className="text-sm text-accanto-500">{user.email}</Text>
      </View>

      <View className="gap-8">
        <LanguageSection />

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

        <ChangePasswordSection />

        <PushDevicesSection />

        <NotificationPreferencesSection />

        <ActiveSessionsSection />

        <TwoFactorSection />

        <SecurityAuditSection />

        <WellbeingSection />

        <ExportSection />

        <View className="gap-3 border-t border-accanto-100 pt-6">
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
