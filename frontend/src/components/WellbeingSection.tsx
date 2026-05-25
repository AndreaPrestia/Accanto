import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { api, extractError } from '../api/client';
import { CaregiverCheckIn } from '../types';

const SCALE = [1, 2, 3, 4, 5] as const;
const TREND_DAYS = 30;

export default function WellbeingSection() {
  const { t, i18n } = useTranslation();
  const [items, setItems] = useState<CaregiverCheckIn[]>([]);
  const [mood, setMood] = useState(3);
  const [energy, setEnergy] = useState(3);
  const [stress, setStress] = useState(3);
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const load = async () => {
    setBusy(true);
    setError(null);
    try {
      const from = new Date(Date.now() - TREND_DAYS * 24 * 3600 * 1000).toISOString();
      const { data } = await api.get<CaregiverCheckIn[]>('/account/check-ins', {
        params: { from, take: 200 }
      });
      setItems(data);
    } catch (e) {
      setError(extractError(e));
    } finally {
      setBusy(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
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
      setSuccess(t('account.wellbeing.saved'));
      await load();
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm(t('account.wellbeing.confirmDelete'))) return;
    setBusy(true);
    try {
      await api.delete(`/account/check-ins/${id}`);
      setItems((prev) => prev.filter((x) => x.id !== id));
    } catch (err) {
      setError(extractError(err));
    } finally {
      setBusy(false);
    }
  };

  const fmtDate = (iso: string) => {
    try {
      return new Date(iso).toLocaleString(i18n.language);
    } catch {
      return iso;
    }
  };

  return (
    <section className="space-y-3">
      <h2 className="text-base font-semibold text-accanto-900">{t('account.wellbeing.title')}</h2>
      <p className="text-sm text-accanto-500">{t('account.wellbeing.hint')}</p>

      {error && <p className="text-sm text-red-700">{error}</p>}
      {success && <p className="text-sm text-green-700">{success}</p>}

      <form onSubmit={handleSubmit} className="space-y-4 card">
        <ScaleField
          label={t('account.wellbeing.mood')}
          value={mood}
          onChange={setMood}
          lowLabel={t('account.wellbeing.moodLow')}
          highLabel={t('account.wellbeing.moodHigh')}
        />
        <ScaleField
          label={t('account.wellbeing.energy')}
          value={energy}
          onChange={setEnergy}
          lowLabel={t('account.wellbeing.energyLow')}
          highLabel={t('account.wellbeing.energyHigh')}
        />
        <ScaleField
          label={t('account.wellbeing.stress')}
          value={stress}
          onChange={setStress}
          lowLabel={t('account.wellbeing.stressLow')}
          highLabel={t('account.wellbeing.stressHigh')}
        />
        <div>
          <label className="block text-sm text-accanto-700 mb-1">{t('account.wellbeing.note')}</label>
          <textarea
            value={note}
            onChange={(e) => setNote(e.target.value)}
            maxLength={500}
            rows={2}
            placeholder={t('account.wellbeing.notePlaceholder')}
            className="w-full border border-accanto-200 rounded-lg px-3 py-2 text-sm"
          />
        </div>
        <button
          type="submit"
          disabled={busy}
          className="w-full sm:w-auto px-4 py-2 rounded-lg bg-accanto-700 text-white text-sm font-medium disabled:opacity-60"
        >
          {t('account.wellbeing.save')}
        </button>
      </form>

      <Trend items={items} t={t} />

      <p className="text-sm text-accanto-500">
        <Link to="/self-care" className="text-accanto-700 underline">
          {t('account.wellbeing.selfCareLink')} →
        </Link>
        {' · '}
        <Link to="/support" className="text-accanto-700 underline">
          {t('account.wellbeing.supportLink')} →
        </Link>
      </p>

      {items.length > 0 && (
        <details className="card">
          <summary className="cursor-pointer text-sm font-medium text-accanto-700">
            {t('account.wellbeing.history', { count: items.length })}
          </summary>
          <ul className="mt-3 space-y-3">
            {items.map((c) => (
              <li key={c.id} className="border-t border-accanto-100 pt-3 first:border-t-0 first:pt-0">
                <div className="text-sm">
                  <strong className="text-accanto-900">{fmtDate(c.createdAt)}</strong>
                  <div className="text-xs text-accanto-500 mt-0.5">
                    {t('account.wellbeing.mood')} {c.mood}/5 · {t('account.wellbeing.energy')} {c.energy}/5 · {t('account.wellbeing.stress')} {c.stress}/5
                  </div>
                </div>
                {c.note && <p className="text-sm text-accanto-700 mt-1">{c.note}</p>}
                <button
                  type="button"
                  className="text-xs text-red-700 underline mt-1"
                  onClick={() => handleDelete(c.id)}
                >
                  {t('common.delete', { defaultValue: 'Elimina' })}
                </button>
              </li>
            ))}
          </ul>
        </details>
      )}
    </section>
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
    <fieldset className="border-0 p-0 m-0">
      <legend className="text-sm font-medium text-accanto-700 mb-2">{label}</legend>
      <div className="flex gap-2">
        {SCALE.map((n) => (
          <button
            key={n}
            type="button"
            className={
              'flex-1 h-10 rounded-lg border text-sm font-medium transition-colors ' +
              (n === value
                ? 'bg-accanto-700 text-white border-accanto-700'
                : 'bg-white text-accanto-700 border-accanto-200 hover:border-accanto-400')
            }
            onClick={() => onChange(n)}
            aria-pressed={n === value}
          >
            {n}
          </button>
        ))}
      </div>
      <div className="flex justify-between mt-1">
        <small className="text-xs text-accanto-500">{lowLabel}</small>
        <small className="text-xs text-accanto-500">{highLabel}</small>
      </div>
    </fieldset>
  );
}

function Trend({
  items,
  t
}: {
  items: CaregiverCheckIn[];
  t: ReturnType<typeof useTranslation>['t'];
}) {
  // ascending order by date
  const sorted = useMemo(() => [...items].sort((a, b) => a.createdAt.localeCompare(b.createdAt)), [items]);

  if (sorted.length < 2) {
    return <p className="text-sm text-accanto-500">{t('account.wellbeing.trendEmpty')}</p>;
  }

  const width = 320;
  const height = 100;
  const padding = 8;
  const xs = sorted.map((_, i) => padding + (i / (sorted.length - 1)) * (width - 2 * padding));
  const yFor = (v: number) => height - padding - ((v - 1) / 4) * (height - 2 * padding);
  const path = (key: 'mood' | 'energy' | 'stress') =>
    sorted.map((c, i) => `${i === 0 ? 'M' : 'L'} ${xs[i].toFixed(1)} ${yFor(c[key]).toFixed(1)}`).join(' ');

  const colors = {
    mood: '#0ea5e9',
    energy: '#16a34a',
    stress: '#dc2626'
  } as const;

  return (
    <div className="card">
      <h3 className="text-sm font-medium text-accanto-700 mb-2">{t('account.wellbeing.trend', { days: TREND_DAYS })}</h3>
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className="w-full h-32"
        role="img"
        aria-label={t('account.wellbeing.trend', { days: TREND_DAYS })}
      >
        <line
          x1={padding}
          y1={yFor(3)}
          x2={width - padding}
          y2={yFor(3)}
          stroke="#e5e7eb"
          strokeWidth={1}
          strokeDasharray="3 3"
        />
        <path d={path('mood')} fill="none" stroke={colors.mood} strokeWidth={2} />
        <path d={path('energy')} fill="none" stroke={colors.energy} strokeWidth={2} />
        <path d={path('stress')} fill="none" stroke={colors.stress} strokeWidth={2} />
      </svg>
      <div className="flex flex-wrap gap-3 mt-2 text-xs">
        <span style={{ color: colors.mood }}>● {t('account.wellbeing.mood')}</span>
        <span style={{ color: colors.energy }}>● {t('account.wellbeing.energy')}</span>
        <span style={{ color: colors.stress }}>● {t('account.wellbeing.stress')}</span>
      </div>
    </div>
  );
}
