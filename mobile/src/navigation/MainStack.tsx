import { createNativeStackNavigator } from '@react-navigation/native-stack';
import type { MainStackParamList } from './types';
import DashboardScreen from '../screens/DashboardScreen';
import NewCircleScreen from '../screens/NewCircleScreen';
import CircleStack from './CircleStack';
import InviteAcceptScreen from '../screens/InviteAcceptScreen';
import WelcomeScreen from '../screens/WelcomeScreen';
import MenuButton from './MenuButton';

const Stack = createNativeStackNavigator<MainStackParamList>();

/**
 * Native stack interno al Drawer: Dashboard è la home, da qui si pushano
 * NewCircle, Circle (che a sua volta apre CircleStack) e InviteAccept.
 *
 * L'header viene gestito qui (non dal Drawer wrapper), così ogni screen
 * mostra il proprio titolo e il bottone hamburger per riaprire il drawer.
 * `Circle` ha headerShown:false perché CircleStack porta già il proprio.
 */
export default function MainStack() {
  return (
    <Stack.Navigator screenOptions={{ headerTitleAlign: 'center' }}>
      <Stack.Screen
        name="Dashboard"
        component={DashboardScreen}
        options={{
          title: 'Le mie care circle',
          headerLeft: () => <MenuButton />
        }}
      />
      <Stack.Screen
        name="Welcome"
        component={WelcomeScreen}
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

