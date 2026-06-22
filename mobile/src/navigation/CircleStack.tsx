import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { getFocusedRouteNameFromRoute, type RouteProp } from '@react-navigation/native';
import type { AppScreenProps, CircleStackParamList, CircleTabParamList } from './types';
import CircleTabs from './CircleTabs';
import { CircleProvider } from './CircleContext';
import DifficultDayScreen from '../screens/DifficultDayScreen';
import AuditScreen from '../screens/AuditScreen';
import AiHistoryScreen from '../screens/AiHistoryScreen';
import MenuButton from './MenuButton';

const Stack = createNativeStackNavigator<CircleStackParamList>();

const tabTitles: Record<keyof CircleTabParamList, string> = {
  CircleOverview: 'Panoramica',
  Timeline: 'Diario',
  Documents: 'Documenti',
  DoctorQuestions: 'Domande',
  SharedUpdates: 'Aggiornamenti'
};

function getTabsHeaderTitle(route: RouteProp<CircleStackParamList, 'CircleTabs'>): string {
  const focused = (getFocusedRouteNameFromRoute(route) ?? 'CircleOverview') as keyof CircleTabParamList;
  return tabTitles[focused] ?? 'Panoramica';
}

/**
 * Stack interno a una care circle: la tab bar è la home, sotto-pagine
 * (difficult day, audit, AI history filtrata sul cerchio) si aprono in push.
 *
 * Riceve `circleId` come route param dal parent MainStack e lo propaga ai
 * children tramite `CircleProvider` (così ogni screen può leggerlo con
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
          options={({ route }) => ({
            headerTitle: getTabsHeaderTitle(route),
            headerLeft: () => <MenuButton />
          })}
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
