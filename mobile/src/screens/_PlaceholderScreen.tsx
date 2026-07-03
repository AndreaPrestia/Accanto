import { View, Text } from 'react-native';

interface PlaceholderProps {
  name: string;
  subtitle?: string;
}

/**
 * Schermo stub usato dai navigatori finché Phase 5 non implementa le pagine
 * vere e proprie. Mantiene lo scheletro di navigazione testabile fin da subito.
 */
export default function PlaceholderScreen({ name, subtitle }: PlaceholderProps) {
  return (
    <View className="flex-1 items-center justify-center bg-accanto-50 px-6">
      <Text className="font-bold text-xl text-accanto-900 mb-1">{name}</Text>
      {subtitle ? (
        <Text className="text-accanto-500 text-center text-sm">{subtitle}</Text>
      ) : null}
    </View>
  );
}
