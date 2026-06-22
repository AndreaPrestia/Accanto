import { useState } from 'react';
import { Modal, Pressable, ScrollView, Text, View } from 'react-native';

export interface SelectOption<T extends string> {
  value: T;
  label: string;
}

interface SelectFieldProps<T extends string> {
  label?: string;
  value: T | '';
  onChange: (value: T | '') => void;
  options: SelectOption<T>[];
  /** Etichetta della voce vuota (es: "Tutti"). Se omessa, l'opzione non viene mostrata. */
  emptyLabel?: string;
  placeholder?: string;
}

/**
 * Componente di selezione con modale full-screen su tap. Pattern semplice
 * basato su `Modal` + lista scrollabile: evita la dipendenza dal Picker
 * nativo che varia molto fra iOS e Android.
 */
export default function SelectField<T extends string>({
  label,
  value,
  onChange,
  options,
  emptyLabel,
  placeholder = 'Seleziona\u2026'
}: SelectFieldProps<T>) {
  const [open, setOpen] = useState(false);

  const currentLabel =
    value === ''
      ? emptyLabel ?? ''
      : options.find((o) => o.value === value)?.label ?? '';

  return (
    <View className="mb-1">
      {label ? (
        <Text className="text-sm font-medium text-accanto-700 mb-1">
          {label}
        </Text>
      ) : null}
      <Pressable
        onPress={() => setOpen(true)}
        className="rounded-md border border-accanto-100 bg-white px-3 py-2.5"
      >
        <Text
          className={`text-base ${
            currentLabel ? 'text-accanto-900' : 'text-accanto-500'
          }`}
          numberOfLines={1}
        >
          {currentLabel || placeholder}
        </Text>
      </Pressable>

      <Modal
        visible={open}
        transparent
        animationType="fade"
        onRequestClose={() => setOpen(false)}
      >
        <Pressable
          className="flex-1 bg-black/40 justify-end"
          onPress={() => setOpen(false)}
        >
          <Pressable
            // Stop propagazione: tap dentro il pannello non chiude.
            onPress={(e) => e.stopPropagation()}
            className="bg-white rounded-t-2xl max-h-[70%]"
          >
            {label ? (
              <Text className="text-base font-semibold text-accanto-900 px-4 pt-4 pb-2">
                {label}
              </Text>
            ) : null}
            <ScrollView className="px-2 pb-4">
              {emptyLabel ? (
                <OptionRow
                  label={emptyLabel}
                  selected={value === ''}
                  onPress={() => {
                    onChange('');
                    setOpen(false);
                  }}
                />
              ) : null}
              {options.map((opt) => (
                <OptionRow
                  key={opt.value}
                  label={opt.label}
                  selected={value === opt.value}
                  onPress={() => {
                    onChange(opt.value);
                    setOpen(false);
                  }}
                />
              ))}
            </ScrollView>
          </Pressable>
        </Pressable>
      </Modal>
    </View>
  );
}

function OptionRow({
  label,
  selected,
  onPress
}: {
  label: string;
  selected: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      onPress={onPress}
      className={`px-3 py-3 rounded-md ${
        selected ? 'bg-accanto-50' : 'bg-white'
      }`}
    >
      <Text
        className={`text-base ${
          selected ? 'font-semibold text-accanto-900' : 'text-accanto-700'
        }`}
      >
        {label}
        {selected ? '  \u2713' : ''}
      </Text>
    </Pressable>
  );
}
