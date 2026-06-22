import { useEffect, useState } from 'react';
import { ActivityIndicator, Text, View } from 'react-native';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { RouteProp } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import ErrorBanner from '../components/ui/ErrorBanner';
import { api, extractError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { setPendingInvite } from '../auth/pendingInvite';
import type { CareCircleInvitePreview } from '@accanto/shared/types';
import { RoleLabel } from '@accanto/shared/types';
import type {
  AuthStackParamList,
  AppStackParamList
} from '../navigation/types';

// La schermata è registrata sia in AuthStack che in AppStack: usiamo i tipi
// di entrambi via union per le route params (token è uguale in tutte e due).
type AnyNav = NativeStackNavigationProp<
  AuthStackParamList & AppStackParamList
>;
type RouteParams = RouteProp<
  { InviteAccept: { token: string } },
  'InviteAccept'
>;

export default function InviteAcceptScreen() {
  const navigation = useNavigation<AnyNav>();
  const route = useRoute<RouteParams>();
  const token = route.params?.token ?? '';
  const { user, loading } = useAuth();

  const [preview, setPreview] = useState<CareCircleInvitePreview | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [accepting, setAccepting] = useState(false);

  useEffect(() => {
    if (!token) return;
    api
      .get<CareCircleInvitePreview>(`/invites/${token}/preview`)
      .then((r) => setPreview(r.data))
      .catch((e) => setError(extractError(e)));
  }, [token]);

  if (!token) {
    return (
      <Screen>
        <Text className="text-sm text-red-700">Link di invito non valido.</Text>
      </Screen>
    );
  }

  if (error && !preview) {
    return (
      <Screen>
        <View className="max-w-md w-full self-center pt-2">
          <Text className="text-xl font-semibold text-accanto-900 mb-3">
            Invito non disponibile
          </Text>
          <ErrorBanner message={error} />
          <View className="mt-4">
            <Button
              variant="ghost"
              onPress={() => {
                if (user) {
                  (navigation as NativeStackNavigationProp<AppStackParamList>).navigate(
                    'AppDrawer'
                  );
                } else {
                  (navigation as NativeStackNavigationProp<AuthStackParamList>).navigate(
                    'Login'
                  );
                }
              }}
            >
              Torna alla home
            </Button>
          </View>
        </View>
      </Screen>
    );
  }

  if (!preview || loading) {
    return (
      <Screen>
        <View className="py-10 items-center">
          <ActivityIndicator color="#334155" />
        </View>
      </Screen>
    );
  }

  const expires = new Date(preview.expiresAt).toLocaleDateString('it-IT', {
    day: '2-digit',
    month: 'long',
    year: 'numeric'
  });

  const accept = async () => {
    setAccepting(true);
    setError(null);
    try {
      const { data } = await api.post<{ careCircleId: string }>(
        `/invites/${token}/accept`,
        {}
      );
      (navigation as NativeStackNavigationProp<AppStackParamList>).reset({
        index: 0,
        routes: [{ name: 'Circle', params: { circleId: data.careCircleId } }]
      });
    } catch (e) {
      setError(extractError(e));
    } finally {
      setAccepting(false);
    }
  };

  return (
    <Screen>
      <View className="max-w-md w-full self-center pt-2">
        <Text className="text-2xl font-semibold text-accanto-900">
          Sei stato invitata/o
        </Text>
        <Text className="text-accanto-500 mt-2">
          <Text className="font-semibold text-accanto-700">
            {preview.invitedByDisplayName}
          </Text>{' '}
          vorrebbe che ti unissi al cerchio di cura{' '}
          <Text className="font-semibold text-accanto-700">
            {preview.circleName}
          </Text>{' '}
          come{' '}
          <Text className="font-semibold text-accanto-700">
            {RoleLabel[preview.role]}
          </Text>
          .
        </Text>
        <Text className="text-xs text-accanto-500 mt-1">
          Il link è valido fino al {expires}.
        </Text>

        <View className="mt-3">
          <ErrorBanner message={error} />
        </View>

        {user ? (
          <View className="mt-6 gap-3">
            <Button onPress={accept} busy={accepting} disabled={accepting}>
              {accepting ? 'Sto entrando…' : 'Entra nel cerchio'}
            </Button>
            <Button
              variant="ghost"
              onPress={() =>
                (navigation as NativeStackNavigationProp<AppStackParamList>).navigate(
                  'AppDrawer'
                )
              }
            >
              Non adesso
            </Button>
          </View>
        ) : (
          <View className="mt-6 gap-3">
            <Text className="text-sm text-accanto-500">
              Per accettare devi prima entrare in Accanto.
            </Text>
            <Button
              onPress={async () => {
                await setPendingInvite(token);
                (navigation as NativeStackNavigationProp<AuthStackParamList>).navigate(
                  'Login'
                );
              }}
            >
              Accedi e accetta
            </Button>
            <Button
              variant="ghost"
              onPress={async () => {
                await setPendingInvite(token);
                (navigation as NativeStackNavigationProp<AuthStackParamList>).navigate(
                  'Register'
                );
              }}
            >
              Non ho un accesso, registrami
            </Button>
          </View>
        )}
      </View>
    </Screen>
  );
}
