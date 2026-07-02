import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import { markWelcomeSeen } from '../storage/welcomeFlag';
import type { AppScreenProps } from '../navigation/types';

type Props = AppScreenProps<'Welcome'>;

/**
 * Onboarding a 3 slide mostrato dopo la registrazione (o al primo focus di
 * Dashboard su un account nuovo). Skippable in qualsiasi momento.
 * Al completamento, apre NewCircle pre-riempito con un nome suggerito.
 */
export default function WelcomeScreen({ navigation }: Props) {
  const { t } = useTranslation();
  const [step, setStep] = useState(0);

  const steps = [
    { title: t('welcome.step1.title'), body: t('welcome.step1.body') },
    { title: t('welcome.step2.title'), body: t('welcome.step2.body') },
    { title: t('welcome.step3.title'), body: t('welcome.step3.body') }
  ];

  const isLast = step === steps.length - 1;
  const current = steps[step];

  const skip = async () => {
    await markWelcomeSeen();
    navigation.replace('Dashboard');
  };

  const next = async () => {
    if (isLast) {
      await markWelcomeSeen();
      navigation.replace('NewCircle', { name: t('welcome.namePrefill') });
      return;
    }
    setStep(step + 1);
  };

  const back = () => {
    if (step > 0) setStep(step - 1);
  };

  return (
    <Screen>
      <View className="max-w-md w-full self-center pt-2">
        <View className="flex-row items-center justify-between mb-6">
          <Text className="text-sm text-accanto-500">
            {t('welcome.step', { current: step + 1, total: steps.length })}
          </Text>
          <Pressable onPress={skip} accessibilityRole="button" className="py-1 px-2 active:opacity-70">
            <Text className="text-sm text-accanto-500 underline">
              {t('welcome.skipCta')}
            </Text>
          </Pressable>
        </View>

        <Text className="text-2xl font-semibold text-accanto-900 mb-2">
          {t('welcome.title')}
        </Text>
        <Text className="text-accanto-500 mb-6">{t('welcome.subtitle')}</Text>

        <View
          className="rounded-xl border border-accanto-100 bg-white p-6 min-h-[220px] justify-center"
          accessibilityLiveRegion="polite"
        >
          <Text className="text-lg font-semibold text-accanto-900 mb-2">
            {current.title}
          </Text>
          <Text className="text-accanto-700 leading-relaxed">
            {current.body}
          </Text>
        </View>

        <View className="mt-4 flex-row items-center justify-center gap-2">
          {steps.map((_, i) => (
            <View
              key={i}
              className={
                'rounded-full ' +
                (i === step
                  ? 'w-6 h-2 bg-accanto-700'
                  : 'w-2 h-2 bg-accanto-200')
              }
            />
          ))}
        </View>

        <View className="mt-6 flex-row items-center justify-between">
          <Pressable
            onPress={back}
            disabled={step === 0}
            accessibilityRole="button"
            className="py-2 px-3 active:opacity-70"
          >
            <Text
              className={
                'text-sm ' +
                (step === 0 ? 'text-accanto-300' : 'text-accanto-700 underline')
              }
            >
              {t('welcome.backCta')}
            </Text>
          </Pressable>
          <View style={{ minWidth: 180 }}>
            <Button onPress={next}>
              {isLast ? t('welcome.ctaCreateFirstCircle') : t('welcome.nextCta')}
            </Button>
          </View>
        </View>
      </View>
    </Screen>
  );
}
