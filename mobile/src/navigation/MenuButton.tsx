import { Pressable, Text } from 'react-native';
import { DrawerActions, useNavigation } from '@react-navigation/native';

/**
 * Bottone hamburger da usare in `headerRight` (o headerLeft) dei navigator
 * annidati dentro l'AppDrawer. Apre il drawer via `getParent('AppDrawer')`
 * così funziona anche da MainStack che da CircleStack (entrambi figli del
 * Drawer registrato con id="AppDrawer").
 */
export default function MenuButton() {
  const navigation = useNavigation();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Apri menu"
      hitSlop={12}
      onPress={() => navigation.getParent('AppDrawer')?.dispatch(DrawerActions.openDrawer())}
      style={{ paddingHorizontal: 8 }}
    >
      <Text style={{ fontSize: 22, color: '#0f172a' }}>{'☰'}</Text>
    </Pressable>
  );
}
