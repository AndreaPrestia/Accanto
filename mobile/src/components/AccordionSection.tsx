import { ReactNode, useEffect, useState } from 'react';
import { Pressable, Text, View } from 'react-native';

interface Props {
  title: string;
  hint?: string | null;
  defaultOpen?: boolean;
  children: ReactNode;
}

/**
 * Accordion "manuale" per React Native: header pressable + body a
 * visibilità condizionale. `defaultOpen` è lo stato iniziale; se cambia
 * dopo il mount (deep link) forziamo apertura, ma non chiusura, così non
 * scavalchiamo l'interazione utente.
 */
export default function AccordionSection({
  title,
  hint,
  defaultOpen,
  children
}: Props) {
  const [open, setOpen] = useState(defaultOpen ?? false);

  useEffect(() => {
    if (defaultOpen) setOpen(true);
  }, [defaultOpen]);

  return (
    <View className="rounded-xl border border-accanto-100 bg-white overflow-hidden">
      <Pressable
        onPress={() => setOpen((v) => !v)}
        accessibilityRole="button"
        accessibilityState={{ expanded: open }}
        className="px-4 py-3 flex-row items-center justify-between active:bg-accanto-50"
      >
        <View className="flex-row items-center gap-2 shrink">
          <Text className="text-base font-semibold text-accanto-900 shrink" numberOfLines={1}>
            {title}
          </Text>
          {hint ? (
            <View className="rounded-full bg-amber-100 px-2 py-0.5">
              <Text className="text-xs text-amber-800">{hint}</Text>
            </View>
          ) : null}
        </View>
        <Text
          accessibilityElementsHidden
          importantForAccessibility="no"
          className="ml-3 text-accanto-500"
        >
          {open ? '▴' : '▾'}
        </Text>
      </Pressable>
      {open ? (
        <View className="px-4 py-4 border-t border-accanto-100 gap-6">
          {children}
        </View>
      ) : null}
    </View>
  );
}
