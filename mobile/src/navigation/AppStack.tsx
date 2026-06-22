import { createNativeStackNavigator } from '@react-navigation/native-stack';
import type { AppStackParamList } from './types';
import AppDrawer from './AppDrawer';
import CircleStack from './CircleStack';
import NewCircleScreen from '../screens/NewCircleScreen';
import InviteAcceptScreen from '../screens/InviteAcceptScreen';

const Stack = createNativeStackNavigator<AppStackParamList>();

/**
 * Stack root per gli utenti autenticati: il drawer è la home, e da lì si
 * apre in push lo stack del singolo cerchio o la creazione di un nuovo cerchio.
 *
 * `InviteAccept` è registrato anche qui: se l'utente è già autenticato e
 * arriva un deep link `accanto://invite/:token`, si apre direttamente.
 */
export default function AppStack() {
  return (
    <Stack.Navigator screenOptions={{ headerTitleAlign: 'center' }}>
      <Stack.Screen
        name="AppDrawer"
        component={AppDrawer}
        options={{ headerShown: false }}
      />
      <Stack.Screen
        name="NewCircle"
        component={NewCircleScreen}
        options={{ title: 'Nuova care circle' }}
      />
      <Stack.Screen
        name="Circle"
        component={CircleStack}
        options={{ headerShown: false }}
      />
      <Stack.Screen
        name="InviteAccept"
        component={InviteAcceptScreen}
        options={{ title: 'Invito' }}
      />
    </Stack.Navigator>
  );
}
