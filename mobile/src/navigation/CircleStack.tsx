import { createNativeStackNavigator } from '@react-navigation/native-stack';
import type { AppScreenProps, CircleStackParamList } from './types';
import CircleTabs from './CircleTabs';
import { CircleProvider } from './CircleContext';
import DifficultDayScreen from '../screens/DifficultDayScreen';
import AuditScreen from '../screens/AuditScreen';
import AiHistoryScreen from '../screens/AiHistoryScreen';

const Stack = createNativeStackNavigator<CircleStackParamList>();

/**
 * Stack interno a una care circle: la tab bar \u00e8 la home, sotto-pagine
 * (difficult day, audit, AI history filtrata sul cerchio) si aprono in push.
 *
 * Riceve `circleId` come route param dal parent AppStack e lo propaga ai
 * children tramite `CircleProvider` (cos\u00ec ogni screen pu\u00f2 leggerlo con
 * `useCircleId()` senza forwarding manuale dei params).
 */
export default function CircleStack({ route }: AppScreenProps<'Circle'>) {
  const { circleId } = route.params;
  return (
    <CircleProvider circleId={circleId}>
      <Stack.Navigator screenOptions={{ headerTitleAlign: 'center' }}>
        <Stack.Screen
          name="CircleTabs"
          component={CircleTabs}
          options={{ headerShown: false }}
        />
        <Stack.Screen
          name="DifficultDay"
          component={DifficultDayScreen}
          options={{ title: 'Giornata difficile' }}
        />
        <Stack.Screen
          name="Audit"
          component={AuditScreen}
          options={{ title: 'Audit log' }}
        />
        <Stack.Screen
          name="AiHistoryCircle"
          component={AiHistoryScreen}
          options={{ title: 'AI history' }}
        />
      </Stack.Navigator>
    </CircleProvider>
  );
}
