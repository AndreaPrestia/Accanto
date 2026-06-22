import { ReactNode } from 'react';
import { Pressable, Text, ActivityIndicator, View } from 'react-native';

type Variant = 'primary' | 'ghost' | 'danger';

interface ButtonProps {
  onPress?: () => void;
  disabled?: boolean;
  busy?: boolean;
  variant?: Variant;
  /** Occupa tutta la larghezza disponibile (default true). */
  fullWidth?: boolean;
  /** Type semantico per accessibilità. */
  accessibilityLabel?: string;
  children: ReactNode;
}

// Mappa varianti → classi NativeWind, in linea con .btn-primary / .btn-ghost web.
const VARIANT_CLASS: Record<Variant, { bg: string; text: string; border: string }> = {
  primary: {
    bg: 'bg-accanto-700 active:bg-accanto-900',
    text: 'text-white',
    border: 'border border-transparent'
  },
  ghost: {
    bg: 'bg-white active:bg-accanto-50',
    text: 'text-accanto-700',
    border: 'border border-accanto-100'
  },
  danger: {
    bg: 'bg-red-700 active:bg-red-800',
    text: 'text-white',
    border: 'border border-transparent'
  }
};

export default function Button({
  onPress,
  disabled,
  busy,
  variant = 'primary',
  fullWidth = true,
  accessibilityLabel,
  children
}: ButtonProps) {
  const isDisabled = disabled || busy;
  const v = VARIANT_CLASS[variant];
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: isDisabled, busy }}
      accessibilityLabel={accessibilityLabel}
      onPress={isDisabled ? undefined : onPress}
      className={`rounded-md px-4 py-3 ${v.bg} ${v.border} ${
        fullWidth ? 'w-full' : ''
      } ${isDisabled ? 'opacity-50' : ''}`}
    >
      <View className="flex-row items-center justify-center gap-2">
        {busy ? (
          <ActivityIndicator
            size="small"
            color={variant === 'ghost' ? '#334155' : '#ffffff'}
          />
        ) : null}
        <Text className={`text-sm font-semibold ${v.text}`}>{children}</Text>
      </View>
    </Pressable>
  );
}
