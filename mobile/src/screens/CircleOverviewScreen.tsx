import { useCallback, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, Text, View } from 'react-native';
import { useFocusEffect, useNavigation } from '@react-navigation/native';
import { useTranslation } from 'react-i18next';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import ErrorBanner from '../components/ui/ErrorBanner';
import InvitesPanel from '../components/InvitesPanel';
import CircleExportPdfButton from '../components/CircleExportPdfButton';
import { api, extractError } from '../api/client';
import { setCircleAiEnabled } from '../api/ai';
import { useAiContext } from '../hooks/useAiContext';
import { useCircleId } from '../navigation/CircleContext';
import type { CareCircle } from '@accanto/shared/types';
import { RoleLabel } from '@accanto/shared/types';
import type { CircleTabScreen } from '../navigation/types';

type Nav = CircleTabScreen<'CircleOverview'>['navigation'];

export default function CircleOverviewScreen() {
  // Navigation è composta (tab + stack circle + stack app): da qui possiamo
  // sia switchare tab (Timeline/Documents/...) sia push DifficultDay/Audit/...
  const navigation = useNavigation<Nav>();
  const circleId = useCircleId();

  const [circle, setCircle] = useState<CareCircle | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const { data } = await api.get<CareCircle>(`/care-circles/${circleId}`);
      setCircle(data);
    } catch (e) {
      setError(extractError(e));
      setCircle(null);
    }
  }, [circleId]);

  useFocusEffect(
    useCallback(() => {
      load();
    }, [load])
  );

  if (error && !circle) {
    return (
      <Screen>
        <ErrorBanner message={error} />
      </Screen>
    );
  }

  if (!circle) {
    return (
      <Screen>
        <View className="py-10 items-center">
          <ActivityIndicator color="#334155" />
        </View>
      </Screen>
    );
  }

  const isOwner = circle.myRole === 'Owner';
  const isActive = circle.status === 'Active';

  return (
    <Screen>
      <Text className="text-2xl font-semibold text-accanto-900">
        {circle.name}
      </Text>
      {circle.description ? (
        <Text className="text-accanto-500 mt-1">{circle.description}</Text>
      ) : null}
      <Text className="text-xs text-accanto-500 mt-2">
        Il tuo ruolo: {RoleLabel[circle.myRole]}
        {circle.status === 'Archived' ? ' • archiviato' : ''}
      </Text>

      <View className="mt-6 gap-3">
        <SectionCard
          title="Diario"
          desc="Annota appuntamenti, sintomi, decisioni."
          onPress={() => navigation.navigate('Timeline')}
        />
        <SectionCard
          title="Documenti"
          desc="Conserva referti, esami, prescrizioni."
          onPress={() => navigation.navigate('Documents')}
        />
        <SectionCard
          title="Domande per il medico"
          desc="Prepara cosa chiedere alla prossima visita."
          onPress={() => navigation.navigate('DoctorQuestions')}
        />
        <SectionCard
          title="Aggiornamenti per gli altri"
          desc="Componi messaggi da copiare e inviare."
          onPress={() => navigation.navigate('SharedUpdates')}
        />
        <SectionCard
          emphasis
          title="Giornata difficile"
          desc="Un piccolo respiro quando serve."
          onPress={() => navigation.navigate('DifficultDay')}
        />
      </View>

      <Pressable
        onPress={() => navigation.navigate('Audit')}
        className="mt-3 py-2"
      >
        <Text className="text-sm text-accanto-500 underline">
          Vedi registro azioni
        </Text>
      </Pressable>

      {isOwner && isActive && circle.aiEnabled ? (
        <Pressable
          onPress={() => navigation.navigate('AiHistoryCircle')}
          className="py-2"
        >
          <Text className="text-sm text-accanto-500 underline">
            AI del cerchio
          </Text>
        </Pressable>
      ) : null}

      {isOwner && isActive ? <InvitesPanel circleId={circle.id} /> : null}

      {isOwner && isActive ? (
        <AiCircleSettingsCard
          circle={circle}
          onChanged={(aiEnabled) => setCircle({ ...circle, aiEnabled })}
        />
      ) : null}

      <CircleExportPdfButton circleId={circle.id} circleName={circle.name} />

      {isOwner && isActive ? (
        <View className="mt-8">
          <ArchiveButton
            id={circle.id}
            onArchived={() => setCircle({ ...circle, status: 'Archived' })}
          />
        </View>
      ) : null}
    </Screen>
  );
}

function SectionCard({
  title,
  desc,
  onPress,
  emphasis
}: {
  title: string;
  desc: string;
  onPress: () => void;
  emphasis?: boolean;
}) {
  return (
    <Pressable
      onPress={onPress}
      className={`rounded-lg border bg-white p-4 active:bg-accanto-50 ${
        emphasis ? 'border-accanto-500' : 'border-accanto-100'
      }`}
    >
      <Text className="font-medium text-accanto-900">{title}</Text>
      <Text className="text-sm text-accanto-500 mt-1">{desc}</Text>
    </Pressable>
  );
}

function ArchiveButton({
  id,
  onArchived
}: {
  id: string;
  onArchived: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const click = () => {
    Alert.alert(
      'Archiviare il cerchio?',
      'Resterà visibile in sola lettura.',
      [
        { text: 'Annulla', style: 'cancel' },
        {
          text: 'Archivia',
          style: 'destructive',
          onPress: async () => {
            setBusy(true);
            try {
              await api.delete(`/care-circles/${id}`);
              onArchived();
            } catch (e) {
              setError(extractError(e));
            } finally {
              setBusy(false);
            }
          }
        }
      ]
    );
  };

  return (
    <View>
      {error ? (
        <Text className="text-sm text-red-700 mb-2">{error}</Text>
      ) : null}
      <Button variant="ghost" onPress={click} busy={busy} disabled={busy}>
        {busy ? 'Archiviazione…' : 'Archivia cerchio'}
      </Button>
    </View>
  );
}

function AiCircleSettingsCard({
  circle,
  onChanged
}: {
  circle: CareCircle;
  onChanged: (enabled: boolean) => void;
}) {
  const { t } = useTranslation();
  const { systemAvailable, loading } = useAiContext();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const toggle = async () => {
    setBusy(true);
    setError(null);
    try {
      const next = !circle.aiEnabled;
      await setCircleAiEnabled(circle.id, next);
      onChanged(next);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <View className="mt-8 rounded-lg border border-accanto-100 bg-white p-4">
      <Text className="font-medium text-accanto-900">{t('ai.title')}</Text>
      <Text className="text-sm text-accanto-500 mt-1">{t('ai.subtitle')}</Text>
      {loading ? (
        <Text className="text-sm text-accanto-500 mt-3">
          {t('common.loading')}
        </Text>
      ) : !systemAvailable ? (
        <Text className="text-sm text-accanto-500 mt-3">
          {t('ai.disabledSystem')}
        </Text>
      ) : (
        <>
          <Pressable
            onPress={toggle}
            disabled={busy}
            className="flex-row items-center gap-2 mt-3"
          >
            <View
              className={`w-5 h-5 rounded border ${
                circle.aiEnabled
                  ? 'bg-accanto-700 border-accanto-700'
                  : 'bg-white border-accanto-500'
              } items-center justify-center`}
            >
              {circle.aiEnabled ? (
                <Text className="text-white text-xs font-bold">✓</Text>
              ) : null}
            </View>
            <Text className="text-sm text-accanto-900 flex-1">
              {t('ai.enableToggle')}
            </Text>
          </Pressable>
          <Text className="text-xs text-accanto-500 mt-1">
            {t('ai.enableHint')}
          </Text>
          {error ? (
            <Text className="text-sm text-red-700 mt-2">{error}</Text>
          ) : null}
        </>
      )}
    </View>
  );
}
