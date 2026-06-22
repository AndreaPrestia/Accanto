import { useMemo, useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { DrawerNavigationProp } from '@react-navigation/drawer';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import TextField from '../components/ui/TextField';
import AiAssistPanel from '../components/AiAssistPanel';
import { checkInReflection } from '../api/ai';
import { useAiContext } from '../hooks/useAiContext';
import { useAuth } from '../auth/AuthContext';
import { dayOfYear } from '@accanto/shared/time/dayOfYear';
import type { AppDrawerParamList } from '../navigation/types';

// Numero di micro-promemoria: deve coincidere con i18n key selfCare.daily.tips[0..N-1].
const TIP_COUNT = 10;

/**
 * Schermata "Cura di te": tip giornaliero rotante (deterministico, basato
 * su dayOfYear), segnali di burnout, riposo, confini, link a Support e
 * sezione AI di riflessione settimanale (CheckInReflection).
 */
export default function SelfCareScreen() {
  const { t } = useTranslation();
  const navigation = useNavigation<DrawerNavigationProp<AppDrawerParamList>>();

  const tipIndex = useMemo(() => dayOfYear(new Date()) % TIP_COUNT, []);
  const signs = t('selfCare.burnout.signs', { returnObjects: true }) as string[];
  const boundaries = t('selfCare.boundaries.points', {
    returnObjects: true
  }) as string[];

  return (
    <Screen>
      <Text className="text-2xl font-semibold text-accanto-900 mb-1">
        {t('selfCare.title') as string}
      </Text>
      <Text className="text-accanto-500 mb-6">
        {t('selfCare.intro') as string}
      </Text>

      <View className="rounded-lg border border-accanto-100 bg-white p-4 mb-4">
        <Text className="font-semibold text-accanto-900 mb-1">
          {t('selfCare.daily.title') as string}
        </Text>
        <Text className="text-accanto-900">
          {t(`selfCare.daily.tips.${tipIndex}`) as string}
        </Text>
      </View>

      <View className="mb-6">
        <Text className="text-lg font-semibold text-accanto-900 mb-2">
          {t('selfCare.burnout.title') as string}
        </Text>
        <Text className="text-sm text-accanto-700 mb-3">
          {t('selfCare.burnout.intro') as string}
        </Text>
        <View className="gap-2 pl-2">
          {signs.map((s, i) => (
            <Text key={i} className="text-sm text-accanto-900">
              \u2022 {s}
            </Text>
          ))}
        </View>
        <Text className="text-sm text-accanto-500 mt-3">
          {t('selfCare.burnout.outro') as string}
        </Text>
      </View>

      <View className="mb-6">
        <Text className="text-lg font-semibold text-accanto-900 mb-2">
          {t('selfCare.rest.title') as string}
        </Text>
        <Text className="text-sm text-accanto-700">
          {t('selfCare.rest.body') as string}
        </Text>
      </View>

      <View className="mb-6">
        <Text className="text-lg font-semibold text-accanto-900 mb-2">
          {t('selfCare.boundaries.title') as string}
        </Text>
        <Text className="text-sm text-accanto-700 mb-2">
          {t('selfCare.boundaries.intro') as string}
        </Text>
        <View className="gap-2 pl-2">
          {boundaries.map((s, i) => (
            <Text key={i} className="text-sm text-accanto-900">
              \u2022 {s}
            </Text>
          ))}
        </View>
      </View>

      <Pressable onPress={() => navigation.navigate('Support')} className="mb-6">
        <Text className="text-sm text-accanto-700 underline">
          {t('selfCare.supportLink') as string} \u2192
        </Text>
      </Pressable>

      <SelfCareAiSection />

      <Text className="mt-6 text-xs text-accanto-500">
        {t('selfCare.disclaimer') as string}
      </Text>
    </Screen>
  );
}

/**
 * Sezione AI: come nel web, `/api/ai/status` richiede l'auth. Nel mobile
 * tutte le rotte sono gi\u00e0 dietro AuthGate, quindi qui basta proteggersi
 * dal caso "utente non ancora caricato" per evitare un 401 prima del
 * rehydrate del token.
 */
function SelfCareAiSection() {
  const { user, loading } = useAuth();
  if (loading || !user) return null;
  return <SelfCareAiSectionInner />;
}

function SelfCareAiSectionInner() {
  const { t } = useTranslation();
  const [days, setDays] = useState('14');
  const { systemAvailable, loading } = useAiContext();

  if (loading) return null;
  const disabled = !systemAvailable;
  const disabledReason = t('ai.disabledSystem') as string;

  return (
    <AiAssistPanel
      title={t('ai.checkInReflection.title') as string}
      description={t('ai.checkInReflection.description') as string}
      ctaLabel={t('ai.checkInReflection.cta') as string}
      disabled={disabled}
      disabledReason={disabledReason}
      onGenerate={() =>
        checkInReflection(Math.max(1, Math.min(90, Number(days) || 14)))
      }
    >
      <TextField
        label={t('ai.checkInReflection.daysLabel') as string}
        value={days}
        onChangeText={setDays}
        keyboardType="number-pad"
      />
    </AiAssistPanel>
  );
}
