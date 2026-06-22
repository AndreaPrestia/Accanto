import { createNativeStackNavigator } from '@react-navigation/native-stack';
import type { CircleStackParamList } from './types';
import CircleTabs from './CircleTabs';
import DifficultDayScreen from '../screens/DifficultDayScreen';
import AuditScreen from '../screens/AuditScreen';
import AiHistoryScreen from '../screens/AiHistoryScreen';

const Stack = createNativeStackNavigator<CircleStackParamList>();

/**
 * Stack interno a una care circle: la tab bar è la home, sotto-pagine
 * (difficult day, audit, AI history filtrata sul cerchio) si aprono in push.
 */
export default function CircleStack() {
  return (
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
  );
}
