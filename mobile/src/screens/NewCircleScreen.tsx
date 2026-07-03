import { useState } from 'react';
import { Text, View } from 'react-native';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import TextField from '../components/ui/TextField';
import ErrorBanner from '../components/ui/ErrorBanner';
import { api, extractError } from '../api/client';
import type { CareCircle } from '@accanto/shared/types';
import type { AppScreenProps } from '../navigation/types';

type Props = AppScreenProps<'NewCircle'>;

export default function NewCircleScreen({ navigation, route }: Props) {
  // Pre-fill "name" arriva dal Welcome finale (o da futuri deep link).
  // Sanifichiamo brutalmente la lunghezza per non caricare pattern strani.
  const initialName = (route.params?.name ?? '').slice(0, 80);
  const [name, setName] = useState(initialName);
  const [description, setDescription] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    const trimmedName = name.trim();
    if (trimmedName.length < 2) {
      setError('Il nome deve contenere almeno 2 caratteri.');
      return;
    }
    setError(null);
    setBusy(true);
    try {
      const { data } = await api.post<CareCircle>('/care-circles', {
        name: trimmedName,
        description: description.trim() || null
      });
      // Sostituiamo la modale corrente con il cerchio appena creato
      // (replace così back non torna a NewCircle vuoto).
      navigation.replace('Circle', { circleId: data.id });
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen>
      <View className="max-w-md w-full self-center pt-2">
        <Text className="text-2xl font-semibold text-accanto-900 mb-2">
          Nuovo cerchio
        </Text>
        <Text className="text-accanto-500 mb-6">
          Dai un nome al cerchio (per esempio il nome della persona che stai
          assistendo). Potrai modificarlo in qualsiasi momento.
        </Text>

        <View className="gap-3">
          <TextField
            label="Nome del cerchio"
            value={name}
            onChangeText={setName}
            placeholder="Es. Mamma"
            autoCapitalize="words"
            maxLength={120}
          />
          <TextField
            label="Descrizione (facoltativa)"
            value={description}
            onChangeText={setDescription}
            placeholder="Una breve nota per orientarti"
            multiline
            numberOfLines={3}
            style={{ minHeight: 80, textAlignVertical: 'top' }}
          />

          <ErrorBanner message={error} />

          <View className="mt-2">
            <Button onPress={submit} busy={busy} disabled={busy}>
              {busy ? 'Creazione…' : 'Crea cerchio'}
            </Button>
          </View>
        </View>
      </View>
    </Screen>
  );
}
