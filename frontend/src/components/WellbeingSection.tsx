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
    <section className="wellbeing-section">
      <h2>{t('account.wellbeing.title')}</h2>
      <p className="muted">{t('account.wellbeing.hint')}</p>

      {error && <p className="error">{error}</p>}
      {success && <p className="success">{success}</p>}

      <form onSubmit={handleSubmit} className="wellbeing-form">
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
        <label className="field">
          <span>{t('account.wellbeing.note')}</span>
          <textarea
            value={note}
            onChange={(e) => setNote(e.target.value)}
            maxLength={500}
            rows={2}
            placeholder={t('account.wellbeing.notePlaceholder')}
          />
        </label>
        <button type="submit" disabled={busy}>
          {t('account.wellbeing.save')}
        </button>
      </form>

      <Trend items={items} t={t} />

      <p className="muted" style={{ marginTop: '1rem' }}>
        <Link to="/support" className="text-accanto-700 underline">
          {t('account.wellbeing.supportLink')} →
        </Link>
      </p>

      {items.length > 0 && (
        <details className="wellbeing-history">
          <summary>{t('account.wellbeing.history', { count: items.length })}</summary>
          <ul>
            {items.map((c) => (
              <li key={c.id}>
                <div>
                  <strong>{fmtDate(c.createdAt)}</strong>
                  <span className="muted">
                    {' '}· {t('account.wellbeing.mood')} {c.mood}/5 · {t('account.wellbeing.energy')} {c.energy}/5 · {t('account.wellbeing.stress')} {c.stress}/5
                  </span>
                </div>
                {c.note && <p>{c.note}</p>}
                <button type="button" className="link-button" onClick={() => handleDelete(c.id)}>
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
    <fieldset className="scale-field">
      <legend>{label}</legend>
      <div className="scale-buttons">
        {SCALE.map((n) => (
          <button
            key={n}
            type="button"
            className={n === value ? 'selected' : ''}
            onClick={() => onChange(n)}
            aria-pressed={n === value}
          >
            {n}
          </button>
        ))}
      </div>
      <div className="scale-labels">
        <small className="muted">{lowLabel}</small>
        <small className="muted">{highLabel}</small>
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
    return <p className="muted">{t('account.wellbeing.trendEmpty')}</p>;
  }

  const width = 320;
  const height = 100;
  const padding = 8;
  const xs = sorted.map((_, i) => padding + (i / (sorted.length - 1)) * (width - 2 * padding));
  const yFor = (v: number) => height - padding - ((v - 1) / 4) * (height - 2 * padding);
  const path = (key: 'mood' | 'energy' | 'stress') =>
    sorted.map((c, i) => `${i === 0 ? 'M' : 'L'} ${xs[i].toFixed(1)} ${yFor(c[key]).toFixed(1)}`).join(' ');

  return (
    <div className="trend-wrap">
      <h3>{t('account.wellbeing.trend', { days: TREND_DAYS })}</h3>
      <svg viewBox={`0 0 ${width} ${height}`} className="trend-svg" role="img" aria-label={t('account.wellbeing.trend', { days: TREND_DAYS })}>
        <line x1={padding} y1={yFor(3)} x2={width - padding} y2={yFor(3)} className="trend-axis" />
        <path d={path('mood')} className="trend-line trend-mood" />
        <path d={path('energy')} className="trend-line trend-energy" />
        <path d={path('stress')} className="trend-line trend-stress" />
      </svg>
      <div className="trend-legend">
        <span className="trend-key trend-mood">● {t('account.wellbeing.mood')}</span>
        <span className="trend-key trend-energy">● {t('account.wellbeing.energy')}</span>
        <span className="trend-key trend-stress">● {t('account.wellbeing.stress')}</span>
      </div>
    </div>
  );
}
