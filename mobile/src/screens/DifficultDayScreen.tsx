import { Pressable, Text, View } from 'react-native';
import Screen from '../components/ui/Screen';
import type { CircleStackScreen } from '../navigation/types';

type Props = CircleStackScreen<'DifficultDay'>;

const SUGGESTIONS = [
  'Concediti tre respiri lenti, senza fare altro.',
  'Bevi un bicchiere d’acqua. Mangia qualcosa, anche poco.',
  'Scrivi una sola frase nel diario, anche dura. Non deve essere bella.',
  'Manda un messaggio a una persona di fiducia: basta “ho una giornata difficile”.',
  'Se puoi, esci cinque minuti. Anche solo sulla soglia.',
  'Non sei sola. Non sei solo. Stai facendo molto.'
];

export default function DifficultDayScreen({ navigation }: Props) {
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
          Giornata difficile
        </Text>
        <Text className="text-accanto-500 mb-6">
          Quando tutto pesa, prova uno di questi piccoli gesti. Non risolvono,
          ma fanno respirare.
        </Text>

        <View className="gap-3">
          {SUGGESTIONS.map((s, i) => (
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
            Se senti di non farcela, parlare con qualcuno aiuta.
          </Text>
          <Pressable onPress={goToSupport} className="py-1">
            <Text className="text-sm text-accanto-700 underline">
              Vedi i contatti di supporto →
            </Text>
          </Pressable>
          <Text className="text-sm text-accanto-500 mt-2">
            Se c’è un’emergenza sanitaria, chiama il 112.
          </Text>
        </View>
      </View>
    </Screen>
  );
}
