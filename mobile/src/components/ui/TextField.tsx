import { forwardRef } from 'react';
import { Text, TextInput, View, TextInputProps } from 'react-native';

interface TextFieldProps extends TextInputProps {
  label?: string;
  /** Messaggio d'errore mostrato sotto al campo. */
  error?: string | null;
  /** Hint mostrato sotto al campo quando non c'è errore. */
  hint?: string;
  /** Aggiunge ID per associare label⇄input (a11y). */
  fieldId?: string;
}

/**
 * TextInput stilato in linea con `.input` lato web. Supporta label, errore
 * e hint. La label è semanticamente collegata via accessibilityLabel.
 */
const TextField = forwardRef<TextInput, TextFieldProps>(function TextField(
  { label, error, hint, fieldId, accessibilityLabel, ...inputProps },
  ref
) {
  const showError = !!error;
  return (
    <View className="mb-1">
      {label ? (
        <Text
          className="text-sm font-medium text-accanto-700 mb-1"
          nativeID={fieldId ? `${fieldId}-label` : undefined}
        >
          {label}
        </Text>
      ) : null}
      <TextInput
        ref={ref}
        accessibilityLabel={accessibilityLabel ?? label}
        placeholderTextColor="#94a3b8"
        className={`w-full rounded-md border bg-white px-3 py-2.5 text-base text-accanto-900 ${
          showError ? 'border-red-500' : 'border-accanto-100'
        }`}
        {...inputProps}
      />
      {showError ? (
        <Text className="text-xs text-red-700 mt-1">{error}</Text>
      ) : hint ? (
        <Text className="text-xs text-accanto-500 mt-1">{hint}</Text>
      ) : null}
    </View>
  );
});

export default TextField;
