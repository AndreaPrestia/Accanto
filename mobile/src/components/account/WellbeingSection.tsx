import { useEffect, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  Pressable,
  Text,
  View
} from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { DrawerNavigationProp } from '@react-navigation/drawer';
import { useTranslation } from 'react-i18next';
import Svg, { Line, Path } from 'react-native-svg';
import Button from '../ui/Button';
import TextField from '../ui/TextField';
import ErrorBanner from '../ui/ErrorBanner';
import { api, extractError } from '../../api/client';
import type { CaregiverCheckIn } from '@accanto/shared/types';
import type { AppDrawerParamList } from '../../navigation/types';

const SCALE = [1, 2, 3, 4, 5] as const;
const TREND_DAYS = 30;

export default function WellbeingSection() {
  const { t, i18n } = useTranslation();
  const navigation =
    useNavigation<DrawerNavigationProp<AppDrawerParamList>>();
  const [items, setItems] = useState<CaregiverCheckIn[]>([]);
  const [mood, setMood] = useState(3);
  const [energy, setEnergy] = useState(3);
  const [stress, setStress] = useState(3);
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const load = async () => {
    setError(null);
    try {
      const from = new Date(
        Date.now() - TREND_DAYS * 24 * 3600 * 1000
      ).toISOString();
      const { data } = await api.get<CaregiverCheckIn[]>(
        '/account/check-ins',
        { params: { from, take: 200 } }
      );
      setItems(data);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const submit = async () => {
    setBusy(true);
    setError(null);
    setSuccess(null);
    try {
      await api.post('/account/check-ins', {
        mood,
        energy,
        stress,
        note: note.trim() || null
      });
      setNote('');
      setMood(3);
      setEnergy(3);
      setStress(3);
      setSuccess(t('account.wellbeing.saved') as string);
      await load();
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = (id: string) => {
    Alert.alert(
      t('account.wellbeing.confirmDelete') as string,
      undefined,
      [
        { text: 'Annulla', style: 'cancel' },
        {
          text: 'Elimina',
          style: 'destructive',
          onPress: async () => {
            try {
              await api.delete(`/account/check-ins/${id}`);
              setItems((prev) => prev.filter((x) => x.id !== id));
            } catch (err) {
              setError(extractError(err));
            }
          }
        }
      ]
    );
  };

  const fmtDate = (iso: string) => {
    try {
      return new Date(iso).toLocaleString(i18n.language);
    } catch {
      return iso;
    }
  };

  return (
    <View className="gap-3">
      <Text className="text-base font-semibold text-accanto-900">
        {t('account.wellbeing.title')}
      </Text>
      <Text className="text-sm text-accanto-500">
        {t('account.wellbeing.hint')}
      </Text>

      <ErrorBanner message={error} />
      {success ? (
        <Text className="text-sm text-green-700">{success}</Text>
      ) : null}

      <View className="rounded-lg border border-accanto-100 bg-white p-4 gap-3">
        <ScaleField
          label={t('account.wellbeing.mood') as string}
          value={mood}
          onChange={setMood}
          lowLabel={t('account.wellbeing.moodLow') as string}
          highLabel={t('account.wellbeing.moodHigh') as string}
        />
        <ScaleField
          label={t('account.wellbeing.energy') as string}
          value={energy}
          onChange={setEnergy}
          lowLabel={t('account.wellbeing.energyLow') as string}
          highLabel={t('account.wellbeing.energyHigh') as string}
        />
        <ScaleField
          label={t('account.wellbeing.stress') as string}
          value={stress}
          onChange={setStress}
          lowLabel={t('account.wellbeing.stressLow') as string}
          highLabel={t('account.wellbeing.stressHigh') as string}
        />
        <TextField
          label={t('account.wellbeing.note') as string}
          value={note}
          onChangeText={setNote}
          multiline
          numberOfLines={2}
          maxLength={500}
          style={{ minHeight: 60, textAlignVertical: 'top' }}
        />
        <Button onPress={submit} busy={busy} disabled={busy}>
          {t('account.wellbeing.save') as string}
        </Button>
      </View>

      {loading ? (
        <ActivityIndicator color="#334155" />
      ) : (
        <Trend items={items} />
      )}

      <View className="flex-row gap-3 flex-wrap">
        <Pressable onPress={() => navigation.navigate('SelfCare')}>
          <Text className="text-sm text-accanto-700 underline">
            {t('account.wellbeing.selfCareLink') as string} \u2192
          </Text>
        </Pressable>
        <Pressable onPress={() => navigation.navigate('Support')}>
          <Text className="text-sm text-accanto-700 underline">
            {t('account.wellbeing.supportLink') as string} \u2192
          </Text>
        </Pressable>
      </View>

      {items.length > 0 ? (
        <View className="rounded-lg border border-accanto-100 bg-white p-4 gap-2">
          <Text className="text-sm font-medium text-accanto-700">
            {t('account.wellbeing.history', { count: items.length }) as string}
          </Text>
          {items.map((c, i) => (
            <View
              key={c.id}
              className={`gap-1 ${i > 0 ? 'border-t border-accanto-100 pt-2' : ''}`}
            >
              <Text className="text-sm font-medium text-accanto-900">
                {fmtDate(c.createdAt)}
              </Text>
              <Text className="text-xs text-accanto-500">
                {t('account.wellbeing.mood')} {c.mood}/5 \u00b7{' '}
                {t('account.wellbeing.energy')} {c.energy}/5 \u00b7{' '}
                {t('account.wellbeing.stress')} {c.stress}/5
              </Text>
              {c.note ? (
                <Text className="text-sm text-accanto-700">{c.note}</Text>
              ) : null}
              <Pressable onPress={() => handleDelete(c.id)}>
                <Text className="text-xs text-red-700 underline">
                  {t('common.delete', { defaultValue: 'Elimina' }) as string}
                </Text>
              </Pressable>
            </View>
          ))}
        </View>
      ) : null}
    </View>
  );
}

function ScaleField({
  label,
  value,
  onChange,
  lowLabel,
  highLabel
}: {
  label: string;
  value: number;
  onChange: (v: number) => void;
  lowLabel: string;
  highLabel: string;
}) {
  return (
    <View>
      <Text className="text-sm font-medium text-accanto-700 mb-2">{label}</Text>
      <View className="flex-row gap-2">
        {SCALE.map((n) => (
          <Pressable
            key={n}
            onPress={() => onChange(n)}
            accessibilityRole="button"
            accessibilityState={{ selected: n === value }}
            className={`flex-1 h-10 rounded-lg border items-center justify-center ${
              n === value
                ? 'bg-accanto-700 border-accanto-700'
                : 'bg-white border-accanto-100'
            }`}
          >
            <Text
              className={`text-sm font-medium ${
                n === value ? 'text-white' : 'text-accanto-700'
              }`}
            >
              {n}
            </Text>
          </Pressable>
        ))}
      </View>
      <View className="flex-row justify-between mt-1">
        <Text className="text-xs text-accanto-500">{lowLabel}</Text>
        <Text className="text-xs text-accanto-500">{highLabel}</Text>
      </View>
    </View>
  );
}

function Trend({ items }: { items: CaregiverCheckIn[] }) {
  const { t } = useTranslation();
  const sorted = useMemo(
    () => [...items].sort((a, b) => a.createdAt.localeCompare(b.createdAt)),
    [items]
  );

  if (sorted.length < 2) {
    return (
      <Text className="text-sm text-accanto-500">
        {t('account.wellbeing.trendEmpty') as string}
      </Text>
    );
  }

  const width = 320;
  const height = 100;
  const padding = 8;
  const xs = sorted.map(
    (_, i) => padding + (i / (sorted.length - 1)) * (width - 2 * padding)
  );
  const yFor = (v: number) =>
    height - padding - ((v - 1) / 4) * (height - 2 * padding);
  const path = (key: 'mood' | 'energy' | 'stress') =>
    sorted
      .map(
        (c, i) =>
          `${i === 0 ? 'M' : 'L'} ${xs[i].toFixed(1)} ${yFor(c[key]).toFixed(1)}`
      )
      .join(' ');

  const colors = {
    mood: '#0ea5e9',
    energy: '#16a34a',
    stress: '#dc2626'
  } as const;

  return (
    <View className="rounded-lg border border-accanto-100 bg-white p-4">
      <Text className="text-sm font-medium text-accanto-700 mb-2">
        {t('account.wellbeing.trend', { days: TREND_DAYS }) as string}
      </Text>
      <Svg
        viewBox={`0 0 ${width} ${height}`}
        width="100%"
        height={120}
        accessibilityLabel={
          t('account.wellbeing.trend', { days: TREND_DAYS }) as string
        }
      >
        <Line
          x1={padding}
          y1={yFor(3)}
          x2={width - padding}
          y2={yFor(3)}
          stroke="#e5e7eb"
          strokeWidth={1}
          strokeDasharray="3 3"
        />
        <Path d={path('mood')} fill="none" stroke={colors.mood} strokeWidth={2} />
        <Path
          d={path('energy')}
          fill="none"
          stroke={colors.energy}
          strokeWidth={2}
        />
        <Path
          d={path('stress')}
          fill="none"
          stroke={colors.stress}
          strokeWidth={2}
        />
      </Svg>
      <View className="flex-row flex-wrap gap-3 mt-2">
        <Text className="text-xs" style={{ color: colors.mood }}>
          \u25cf {t('account.wellbeing.mood') as string}
        </Text>
        <Text className="text-xs" style={{ color: colors.energy }}>
          \u25cf {t('account.wellbeing.energy') as string}
        </Text>
        <Text className="text-xs" style={{ color: colors.stress }}>
          \u25cf {t('account.wellbeing.stress') as string}
        </Text>
      </View>
    </View>
  );
}
