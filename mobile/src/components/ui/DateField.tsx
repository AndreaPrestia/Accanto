import { useState } from 'react';
import { Platform, Pressable, Text, View } from 'react-native';
import DateTimePicker from '@react-native-community/datetimepicker';

interface DateFieldProps {
  label?: string;
  /** ISO string in UTC, oppure stringa vuota. */
  value: string;
  onChange: (iso: string) => void;
  mode?: 'date' | 'datetime';
  /** Vincoli opzionali. ISO string. */
  minimumDate?: string;
  maximumDate?: string;
  /** Mostra il pulsante "Pulisci" che setta a stringa vuota. */
  clearable?: boolean;
  placeholder?: string;
}

function formatDisplay(iso: string, mode: 'date' | 'datetime'): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (mode === 'date') {
    return d.toLocaleDateString('it-IT', {
      day: '2-digit',
      month: 'long',
      year: 'numeric'
    });
  }
  return d.toLocaleString('it-IT', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
}

/**
 * Wrapper su `@react-native-community/datetimepicker` con look in linea con
 * `TextField`. Su iOS apre uno spinner inline (dismiss via onConfirm), su
 * Android apre il dialog di sistema (e si richiude da solo).
 *
 * - `mode='date'` mostra solo data.
 * - `mode='datetime'` mostra date + time in due passaggi su Android, picker
 *   combinato su iOS.
 */
export default function DateField({
  label,
  value,
  onChange,
  mode = 'date',
  minimumDate,
  maximumDate,
  clearable,
  placeholder = 'Seleziona\u2026'
}: DateFieldProps) {
  const [showDate, setShowDate] = useState(false);
  const [showTime, setShowTime] = useState(false);

  const current = value ? new Date(value) : new Date();
  const min = minimumDate ? new Date(minimumDate) : undefined;
  const max = maximumDate ? new Date(maximumDate) : undefined;

  const handleDateChange = (event: { type?: string }, selected?: Date) => {
    // Android chiude il dialog automaticamente; gestisci dismiss esplicito.
    if (Platform.OS === 'android') {
      setShowDate(false);
      if (event.type === 'dismissed' || !selected) return;
      if (mode === 'datetime') {
        // Apri il time picker subito dopo aver scelto la data.
        // Conserviamo la data scelta ma azzeriamo a 0 secondi/ms.
        const d = new Date(selected);
        d.setSeconds(0, 0);
        // Memorizziamo temporaneamente nello state attraverso onChange,
        // poi il time picker la rifiner\u00e0.
        onChange(d.toISOString());
        setShowTime(true);
      } else {
        onChange(selected.toISOString());
      }
      return;
    }
    // iOS: aggiorna in tempo reale.
    if (selected) onChange(selected.toISOString());
  };

  const handleTimeChange = (event: { type?: string }, selected?: Date) => {
    if (Platform.OS === 'android') {
      setShowTime(false);
      if (event.type === 'dismissed' || !selected) return;
      onChange(selected.toISOString());
      return;
    }
    if (selected) onChange(selected.toISOString());
  };

  return (
    <View className="mb-1">
      {label ? (
        <Text className="text-sm font-medium text-accanto-700 mb-1">
          {label}
        </Text>
      ) : null}
      <View className="flex-row items-center gap-2">
        <Pressable
          onPress={() => setShowDate(true)}
          className="flex-1 rounded-md border border-accanto-100 bg-white px-3 py-2.5"
        >
          <Text
            className={`text-base ${
              value ? 'text-accanto-900' : 'text-accanto-500'
            }`}
          >
            {value ? formatDisplay(value, mode) : placeholder}
          </Text>
        </Pressable>
        {clearable && value ? (
          <Pressable
            onPress={() => onChange('')}
            className="px-3 py-2.5 rounded-md border border-accanto-100 bg-white"
          >
            <Text className="text-sm text-accanto-700">Pulisci</Text>
          </Pressable>
        ) : null}
      </View>

      {showDate ? (
        <DateTimePicker
          value={current}
          mode={mode === 'datetime' && Platform.OS === 'ios' ? 'datetime' : 'date'}
          display={Platform.OS === 'ios' ? 'spinner' : 'default'}
          minimumDate={min}
          maximumDate={max}
          onChange={handleDateChange}
        />
      ) : null}

      {showTime ? (
        <DateTimePicker
          value={current}
          mode="time"
          display={Platform.OS === 'ios' ? 'spinner' : 'default'}
          onChange={handleTimeChange}
        />
      ) : null}

      {/* iOS: spinner inline, mostra un bottone "Fatto" sotto. */}
      {showDate && Platform.OS === 'ios' ? (
        <Pressable
          onPress={() => setShowDate(false)}
          className="mt-2 self-end px-3 py-2"
        >
          <Text className="text-sm font-semibold text-accanto-700">Fatto</Text>
        </Pressable>
      ) : null}
    </View>
  );
}
