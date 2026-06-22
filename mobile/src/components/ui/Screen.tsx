import { ReactNode } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  View,
  ViewStyle,
  StyleProp
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

interface ScreenProps {
  children: ReactNode;
  /** Disattiva lo ScrollView (utile per liste interne FlatList/SectionList). */
  scroll?: boolean;
  /** Padding orizzontale interno. Default: px-4 */
  padded?: boolean;
  contentContainerStyle?: StyleProp<ViewStyle>;
}

/**
 * Layout standard per ogni screen: SafeArea + KeyboardAvoidingView + ScrollView
 * con padding orizzontale e fondo `accanto-50`. Componente in stile
 * `AppShell` lato web, ma senza header (lo gestisce React Navigation).
 */
export default function Screen({
  children,
  scroll = true,
  padded = true,
  contentContainerStyle
}: ScreenProps) {
  const inner = padded ? (
    <View className="flex-1 px-4 py-4">{children}</View>
  ) : (
    <View className="flex-1">{children}</View>
  );

  return (
    <SafeAreaView edges={['bottom', 'left', 'right']} className="flex-1 bg-accanto-50">
      <KeyboardAvoidingView
        style={{ flex: 1 }}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        {scroll ? (
          <ScrollView
            keyboardShouldPersistTaps="handled"
            contentContainerStyle={[{ flexGrow: 1 }, contentContainerStyle]}
          >
            {inner}
          </ScrollView>
        ) : (
          inner
        )}
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
