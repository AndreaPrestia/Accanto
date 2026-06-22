import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Pressable, Text, View } from 'react-native';
import { useFocusEffect, useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import ErrorBanner from '../components/ui/ErrorBanner';
import { api, extractError } from '../api/client';
import type { CareCircle } from '@accanto/shared/types';
import { RoleLabel } from '@accanto/shared/types';
import type { MainStackParamList } from '../navigation/types';

type Nav = NativeStackNavigationProp<MainStackParamList>;

export default function DashboardScreen() {
  // useNavigation tipizzata sull'AppStack (parente del drawer): da qui
  // possiamo aprire `Circle` (push) e `NewCircle`.
  const navigation = useNavigation<Nav>();
  const [circles, setCircles] = useState<CareCircle[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const { data } = await api.get<CareCircle[]>('/care-circles');
      setCircles(data);
    } catch (e) {
      setError(extractError(e));
      setCircles([]);
    }
  }, []);

  // Ricarica ogni volta che la schermata torna in focus (e al primo mount).
  useFocusEffect(
    useCallback(() => {
      load();
    }, [load])
  );

  // Cleanup non necessario: useFocusEffect lo gestisce.
  useEffect(() => {
    // intenzionalmente vuoto: evita warning su dipendenze
  }, []);

  return (
    <Screen>
      <Text className="text-2xl font-semibold text-accanto-900 mb-1">
        Il tuo spazio
      </Text>
      <Text className="text-accanto-500 mb-6">
        Un cerchio di cura raccoglie le informazioni sulla persona che stai
        assistendo, in un solo posto.
      </Text>

      <View className="mb-3">
        <ErrorBanner message={error} />
      </View>

      {circles === null ? (
        <View className="py-6 items-center">
          <ActivityIndicator color="#334155" />
        </View>
      ) : circles.length === 0 ? (
        <View className="rounded-lg border border-accanto-100 bg-white p-4 mb-4">
          <Text className="text-accanto-900 mb-3">
            Non hai ancora creato nessun cerchio.
          </Text>
          <Button onPress={() => navigation.navigate('NewCircle')}>
            Crea il primo cerchio
          </Button>
        </View>
      ) : (
        <>
          <View className="gap-3 mb-4">
            {circles.map((c) => (
              <Pressable
                key={c.id}
                onPress={() => navigation.navigate('Circle', { circleId: c.id })}
                className="rounded-lg border border-accanto-100 bg-white p-4 active:bg-accanto-50"
              >
                <View className="flex-row items-baseline justify-between">
                  <Text className="text-lg font-medium text-accanto-900" numberOfLines={1}>
                    {c.name}
                  </Text>
                  <Text className="text-xs text-accanto-500 ml-2">
                    {RoleLabel[c.myRole]}
                  </Text>
                </View>
                {c.description ? (
                  <Text className="text-sm text-accanto-500 mt-1" numberOfLines={3}>
                    {c.description}
                  </Text>
                ) : null}
                {c.status === 'Archived' ? (
                  <Text className="text-xs text-accanto-500 mt-2">Archiviato</Text>
                ) : null}
              </Pressable>
            ))}
          </View>
          <Button
            variant="ghost"
            onPress={() => navigation.navigate('NewCircle')}
          >
            + Nuovo cerchio
          </Button>
        </>
      )}
    </Screen>
  );
}
