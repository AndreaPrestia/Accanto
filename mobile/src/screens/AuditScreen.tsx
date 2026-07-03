import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Text, View } from 'react-native';
import Screen from '../components/ui/Screen';
import Button from '../components/ui/Button';
import ErrorBanner from '../components/ui/ErrorBanner';
import { api, extractError } from '../api/client';
import { useCircleId } from '../navigation/CircleContext';

type AuditEntry = {
  id: string;
  careCircleId: string;
  performedByUserId: string;
  performedByDisplayName: string | null;
  actionType: string;
  resourceType: string;
  resourceId: string | null;
  summary: string | null;
  timestamp: string;
};

type Page = {
  items: AuditEntry[];
  total: number;
  skip: number;
  take: number;
};

const PAGE_SIZE = 50;

const ACTION_LABEL: Record<string, string> = {
  CircleCreated: 'Ha creato il cerchio',
  CircleUpdated: 'Ha modificato il cerchio',
  CircleArchived: 'Ha archiviato il cerchio',
  MemberJoined: 'È entrato nel cerchio',
  InviteCreated: 'Ha creato un invito',
  InviteRevoked: 'Ha revocato un invito',
  EntryCreated: 'Ha aggiunto una voce al diario',
  EntryUpdated: 'Ha modificato una voce del diario',
  EntryDeleted: 'Ha eliminato una voce del diario',
  EntriesBulkUpdated: 'Ha aggiornato più voci del diario',
  DocumentUploaded: 'Ha caricato un documento',
  DocumentDeleted: 'Ha eliminato un documento',
  QuestionCreated: 'Ha aggiunto una domanda per il medico',
  QuestionUpdated: 'Ha modificato una domanda per il medico',
  QuestionDeleted: 'Ha eliminato una domanda per il medico',
  UpdateCreated: 'Ha pubblicato un aggiornamento',
  UpdateDeleted: 'Ha eliminato un aggiornamento',
  DataExported: 'Ha esportato i propri dati'
};

export default function AuditScreen() {
  const circleId = useCircleId();
  const [items, setItems] = useState<AuditEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [skip, setSkip] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(
    async (from: number, replace: boolean) => {
      setLoading(true);
      setError(null);
      try {
        const { data } = await api.get<Page>(
          `/care-circles/${circleId}/audit`,
          { params: { skip: from, take: PAGE_SIZE } }
        );
        setTotal(data.total);
        setSkip(from + data.items.length);
        setItems((prev) => (replace ? data.items : [...prev, ...data.items]));
      } catch (e) {
        setError(extractError(e));
      } finally {
        setLoading(false);
      }
    },
    [circleId]
  );

  useEffect(() => {
    load(0, true);
  }, [load]);

  return (
    <Screen>
      <Text className="text-2xl font-semibold text-accanto-900 mb-1">
        Registro azioni
      </Text>
      <Text className="text-accanto-500 mb-4">
        Le azioni svolte dai membri del cerchio, dalla più recente.
      </Text>

      <ErrorBanner message={error} />

      {items.length === 0 && !loading ? (
        <Text className="text-accanto-500">Nessuna azione registrata.</Text>
      ) : (
        <View className="gap-2">
          {items.map((e) => (
            <View
              key={e.id}
              className="rounded-lg border border-accanto-100 bg-white p-3"
            >
              <Text className="text-sm text-accanto-900">
                <Text className="font-medium">
                  {e.performedByDisplayName ?? 'Membro rimosso'}
                </Text>
                {' — '}
                <Text>{ACTION_LABEL[e.actionType] ?? e.actionType}</Text>
              </Text>
              {e.summary ? (
                <Text className="text-sm text-accanto-700 mt-1">
                  {e.summary}
                </Text>
              ) : null}
              <Text className="text-xs text-accanto-500 mt-1">
                {new Date(e.timestamp).toLocaleString('it-IT')}
              </Text>
            </View>
          ))}
        </View>
      )}

      {skip < total ? (
        <View className="mt-4">
          <Button
            variant="ghost"
            onPress={() => load(skip, false)}
            disabled={loading}
            busy={loading}
          >
            {loading ? 'Caricamento…' : 'Carica altre'}
          </Button>
        </View>
      ) : null}
      {loading && items.length === 0 ? (
        <ActivityIndicator color="#334155" />
      ) : null}
    </Screen>
  );
}
