import { Pressable, Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import type { CircleStackScreen } from '../navigation/types';

type Props = CircleStackScreen<'DifficultDay'>;

export default function DifficultDayScreen({ navigation }: Props) {
  const { t } = useTranslation();
  const suggestions = t('difficultDay.suggestions', {
    returnObjects: true
  }) as string[];
  // Naviga al Support, che ora vive direttamente come voce del drawer
  // sibling di Main (vedi AppDrawer). I tipi compositi di CircleStackScreen
  // includono il drawer parente.
  const goToSupport = () => {
    navigation.navigate('Support');
  };

  return (
    <Screen>
      <View className="max-w-md w-full self-center pt-2">
        <Text className="text-2xl font-semibold text-accanto-900 mb-2">
          {t('difficultDay.title')}
        </Text>
        <Text className="text-accanto-500 mb-6">
          {t('difficultDay.intro')}
        </Text>

        <View className="gap-3">
          {suggestions.map((s, i) => (
            <View
              key={i}
              className="rounded-lg border border-accanto-100 bg-white p-4"
            >
              <Text className="text-accanto-900">
                {i + 1}. {s}
              </Text>
            </View>
          ))}
        </View>

        <View className="mt-8">
          <Text className="text-sm text-accanto-500 mb-2">
            {t('difficultDay.needHelp')}
          </Text>
          <Pressable onPress={goToSupport} className="py-1">
            <Text className="text-sm text-accanto-700 underline">
              {t('difficultDay.supportLink')} →
            </Text>
          </Pressable>
          <Text className="text-sm text-accanto-500 mt-2">
            {t('difficultDay.emergency')}
          </Text>
        </View>
      </View>
    </Screen>
  );
}
