import { Text, View } from 'react-native';

interface ErrorBannerProps {
  message?: string | null;
}

/**
 * Banner d'errore in linea con lo stile web (`text-sm text-red-700 bg-red-50 border border-red-200`).
 * Ritorna `null` se non c'è nessun messaggio così il chiamante può usarlo
 * direttamente come `<ErrorBanner message={error} />`.
 */
export default function ErrorBanner({ message }: ErrorBannerProps) {
  if (!message) return null;
  return (
    <View
      accessibilityRole="alert"
      className="rounded-md border border-red-200 bg-red-50 px-3 py-2"
    >
      <Text className="text-sm text-red-700">{message}</Text>
    </View>
  );
}
