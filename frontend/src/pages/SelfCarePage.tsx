import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

// Numero di micro-promemoria: deve coincidere con i18n key selfCare.daily.tips[0..N-1].
const TIP_COUNT = 10;

function dayOfYear(d: Date): number {
  const start = new Date(d.getFullYear(), 0, 0);
  const diff = d.getTime() - start.getTime();
  return Math.floor(diff / 86_400_000);
}

export default function SelfCarePage() {
  const { t } = useTranslation();

  const tipIndex = useMemo(() => dayOfYear(new Date()) % TIP_COUNT, []);
  const signs = t('selfCare.burnout.signs', { returnObjects: true }) as string[];
  const boundaries = t('selfCare.boundaries.points', { returnObjects: true }) as string[];

  return (
    <div className="max-w-md mx-auto pt-2">
      <div className="flex items-center justify-between mb-2">
        <h1 className="text-2xl font-semibold">{t('selfCare.title')}</h1>
        <Link to="/" className="text-sm text-accanto-500 hover:underline">
          ← {t('nav.home', { defaultValue: 'Home' })}
        </Link>
      </div>
      <p className="text-accanto-500 mb-6">{t('selfCare.intro')}</p>

      <section className="card mb-4">
        <h2 className="font-semibold mb-1">{t('selfCare.daily.title')}</h2>
        <p>{t(`selfCare.daily.tips.${tipIndex}`)}</p>
      </section>

      <section className="mb-6">
        <h2 className="text-lg font-semibold mb-2">{t('selfCare.burnout.title')}</h2>
        <p className="text-sm text-accanto-700 mb-3">{t('selfCare.burnout.intro')}</p>
        <ul className="space-y-2 list-disc list-inside text-sm">
          {signs.map((s, i) => (
            <li key={i}>{s}</li>
          ))}
        </ul>
        <p className="text-sm text-accanto-500 mt-3">{t('selfCare.burnout.outro')}</p>
      </section>

      <section className="mb-6">
        <h2 className="text-lg font-semibold mb-2">{t('selfCare.rest.title')}</h2>
        <p className="text-sm text-accanto-700">{t('selfCare.rest.body')}</p>
      </section>

      <section className="mb-6">
        <h2 className="text-lg font-semibold mb-2">{t('selfCare.boundaries.title')}</h2>
        <p className="text-sm text-accanto-700 mb-2">{t('selfCare.boundaries.intro')}</p>
        <ul className="space-y-2 list-disc list-inside text-sm">
          {boundaries.map((s, i) => (
            <li key={i}>{s}</li>
          ))}
        </ul>
      </section>

      <p className="text-sm text-accanto-500">
        <Link to="/support" className="text-accanto-700 underline">
          {t('selfCare.supportLink')} →
        </Link>
      </p>

      <p className="mt-8 text-xs text-accanto-500">{t('selfCare.disclaimer')}</p>
    </div>
  );
}
