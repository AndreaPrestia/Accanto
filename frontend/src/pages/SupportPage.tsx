import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  SUPPORT_CATEGORIES,
  SUPPORT_RESOURCES,
  type SupportCategory,
  type SupportResource
} from '../data/supportResources';

export default function SupportPage() {
  const { t } = useTranslation();
  const [active, setActive] = useState<SupportCategory | 'all'>('all');

  const items = useMemo(() => {
    if (active === 'all') return SUPPORT_RESOURCES;
    return SUPPORT_RESOURCES.filter((r) => r.category === active);
  }, [active]);

  return (
    <div className="max-w-md mx-auto pt-2">
      <div className="flex items-center justify-between mb-2">
        <h1 className="text-2xl font-semibold">{t('support.title')}</h1>
        <Link to="/" className="text-sm text-accanto-500 hover:underline">
          ← {t('nav.home', { defaultValue: 'Home' })}
        </Link>
      </div>
      <p className="text-accanto-500 mb-4">{t('support.intro')}</p>

      <div className="flex flex-wrap gap-2 mb-4" role="tablist" aria-label={t('support.filterAria')}>
        <CategoryChip
          active={active === 'all'}
          onClick={() => setActive('all')}
          label={t('support.categories.all')}
        />
        {SUPPORT_CATEGORIES.map((cat) => (
          <CategoryChip
            key={cat}
            active={active === cat}
            onClick={() => setActive(cat)}
            label={t(`support.categories.${cat}`)}
          />
        ))}
      </div>

      <ul className="space-y-3">
        {items.map((r) => (
          <ResourceCard key={r.id} resource={r} t={t} />
        ))}
      </ul>

      <p className="mt-8 text-xs text-accanto-500">{t('support.disclaimer')}</p>
    </div>
  );
}

function CategoryChip({
  active,
  onClick,
  label
}: {
  active: boolean;
  onClick: () => void;
  label: string;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={
        'px-3 py-1 rounded-full text-sm border ' +
        (active
          ? 'bg-accanto-700 text-white border-accanto-700'
          : 'bg-white text-accanto-700 border-accanto-200 hover:border-accanto-400')
      }
    >
      {label}
    </button>
  );
}

function ResourceCard({
  resource,
  t
}: {
  resource: SupportResource;
  t: ReturnType<typeof useTranslation>['t'];
}) {
  return (
    <li className="card">
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="font-semibold">{resource.name}</h2>
        <span className="text-xs text-accanto-500 shrink-0">
          {t(`support.categories.${resource.category}`)}
        </span>
      </div>
      <p className="text-sm text-accanto-700 mt-1">{resource.description}</p>
      {resource.hours && (
        <p className="text-xs text-accanto-500 mt-1">
          <span className="font-medium">{t('support.hours')}:</span> {resource.hours}
        </p>
      )}
      <div className="mt-2 flex flex-wrap gap-3 text-sm">
        {resource.phone && (
          <a
            href={`tel:${resource.phone}`}
            className="text-accanto-700 underline"
            aria-label={`${t('support.call')} ${resource.name}`}
          >
            {t('support.call')}: {resource.phoneLabel ?? resource.phone}
          </a>
        )}
        {resource.url && (
          <a
            href={resource.url}
            target="_blank"
            rel="noopener noreferrer"
            className="text-accanto-700 underline"
          >
            {t('support.website')} ↗
          </a>
        )}
      </div>
    </li>
  );
}
