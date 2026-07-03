import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import { CareCircle, RoleLabel } from '../types';
import SecurityBanner from '../components/SecurityBanner';

export default function DashboardPage() {
  const { t } = useTranslation();
  const [circles, setCircles] = useState<CareCircle[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.get<CareCircle[]>('/care-circles')
      .then((r) => setCircles(r.data))
      .catch((e) => setError(extractError(e)));
  }, []);

  return (
    <div>
      <h1 className="text-2xl font-semibold mb-1">Il tuo spazio</h1>
      <p className="text-accanto-500 mb-6">
        Un cerchio di cura raccoglie le informazioni sulla persona che stai assistendo, in un solo posto.
      </p>

      <SecurityBanner />

      {error && <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2 mb-4">{error}</div>}

      {circles === null && <p className="text-accanto-500">Caricamento…</p>}

      {circles && circles.length === 0 && (
        <div className="card mb-4">
          <p className="mb-3">Non hai ancora creato nessun cerchio.</p>
          <Link to="/care-circles/new" className="btn-primary">Crea il primo cerchio</Link>
        </div>
      )}

      {circles && circles.length > 0 && (
        <>
          <div className="space-y-3 mb-4">
            {circles.map((c) => (
              <div key={c.id} className="card">
                <div className="flex items-baseline justify-between">
                  <Link to={`/care-circles/${c.id}`} className="text-lg font-medium text-accanto-900 hover:underline">
                    {c.name}
                  </Link>
                  <span className="text-xs text-accanto-500 ml-2">{RoleLabel[c.myRole]}</span>
                </div>
                {c.description && <p className="text-sm text-accanto-500 mt-1">{c.description}</p>}
                {c.status === 'Archived' && <p className="text-xs text-accanto-500 mt-2">Archiviato</p>}
                {c.status !== 'Archived' && (
                  <div className="mt-3 flex justify-end">
                    <Link
                      to={`/care-circles/${c.id}/timeline?new=1`}
                      className="text-sm text-accanto-700 hover:underline"
                      aria-label={t('dashboard.quickAddEntryAria', { name: c.name })}
                    >
                      {t('dashboard.quickAddEntry')}
                    </Link>
                  </div>
                )}
              </div>
            ))}
          </div>
          <Link to="/care-circles/new" className="btn-ghost">+ Nuovo cerchio</Link>
        </>
      )}
    </div>
  );
}
