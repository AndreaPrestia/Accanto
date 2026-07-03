import { createDrawerNavigator, DrawerContentScrollView, DrawerItem, DrawerItemList } from '@react-navigation/drawer';
import { View, Text } from 'react-native';
import type { DrawerContentComponentProps } from '@react-navigation/drawer';
import type { AppDrawerParamList } from './types';
import MainStack from './MainStack';
import AccountScreen from '../screens/AccountScreen';
import AiHistoryScreen from '../screens/AiHistoryScreen';
import SupportScreen from '../screens/SupportScreen';
import SelfCareScreen from '../screens/SelfCareScreen';
import { useAuth } from '../auth/AuthContext';

const Drawer = createDrawerNavigator<AppDrawerParamList>();

function DrawerContent(props: DrawerContentComponentProps) {
  const { user, logout } = useAuth();
  return (
    <DrawerContentScrollView {...props}>
      <View className="px-4 pb-4 mb-2 border-b border-accanto-100">
        <Text className="font-bold text-accanto-900 text-base" numberOfLines={1}>
          {user?.displayName ?? user?.email ?? 'Accanto'}
        </Text>
        {user?.email && user.displayName ? (
          <Text className="text-accanto-500 text-xs mt-0.5" numberOfLines={1}>
            {user.email}
          </Text>
        ) : null}
      </View>
      <DrawerItemList {...props} />
      <DrawerItem
        label="Logout"
        labelStyle={{ color: '#0f172a', fontWeight: '600' }}
        onPress={() => {
          logout().catch(() => {
            /* ignore */
          });
        }}
      />
    </DrawerContentScrollView>
  );
}

/**
 * Drawer root per gli utenti autenticati.
 *
 * - `Main` (native stack: Dashboard, NewCircle, Circle, InviteAccept) è
 *   l'esperienza primaria, headerShown:false perché il MainStack porta
 *   il proprio header con hamburger.
 * - Le altre voci sono pagine "globali" raggiungibili dal drawer da
 *   qualunque punto della app, anche da dentro un cerchio.
 *
 * `id="AppDrawer"` permette ai discendenti (MainStack, CircleStack) di
 * recuperare il drawer via `useNavigation().getParent('AppDrawer')` per
 * aprirlo dal bottone hamburger nei loro header.
 */
export default function AppDrawer() {
  return (
    <Drawer.Navigator
      id="AppDrawer"
      drawerContent={DrawerContent}
      screenOptions={{
        headerTitleAlign: 'center',
        drawerActiveTintColor: '#0f172a',
        drawerInactiveTintColor: '#475569'
      }}
    >
      <Drawer.Screen
        name="Main"
        component={MainStack}
        options={{ title: 'Le mie care circle', headerShown: false }}
      />
      <Drawer.Screen
        name="Account"
        component={AccountScreen}
        options={{ title: 'Account' }}
      />
      <Drawer.Screen
        name="AiHistory"
        component={AiHistoryScreen}
        options={{ title: 'AI history' }}
      />
      <Drawer.Screen
        name="SelfCare"
        component={SelfCareScreen}
        options={{ title: 'Prenditi cura di te' }}
      />
      <Drawer.Screen
        name="Support"
        component={SupportScreen}
        options={{ title: 'Supporto' }}
      />
    </Drawer.Navigator>
  );
}

