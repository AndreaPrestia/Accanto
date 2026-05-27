import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import AiAssistPanel from './AiAssistPanel';
import { checkInReflection } from '../api/ai';
import { useAiContext } from '../hooks/useAiContext';
import { useAuth } from '../auth/AuthContext';

// /self-care è una pagina pubblica ma `/api/ai/status` è `[Authorize]`:
// se montassimo SelfCareAiSectionInner per gli anonimi, l'interceptor
// axios reagirebbe al 401 con un redirect forzato a /login. Mostriamo
// la sezione AI solo dopo che AuthContext ha confermato un utente.
export default function SelfCareAiSection() {
  const { user, loading } = useAuth();
  if (loading || !user) return null;
  return <SelfCareAiSectionInner />;
}

function SelfCareAiSectionInner() {
  const { t } = useTranslation();
  const [days, setDays] = useState(14);
  const { systemAvailable, loading } = useAiContext();

  if (loading) return null;
  const disabled = !systemAvailable;
  const disabledReason = t('ai.disabledSystem') as string;

  return (
    <AiAssistPanel
      title={t('ai.checkInReflection.title')}
      description={t('ai.checkInReflection.description') as string}
      ctaLabel={t('ai.checkInReflection.cta')}
      disabled={disabled}
      disabledReason={disabledReason}
      onGenerate={() => checkInReflection(days)}
    >
      <label className="text-sm">
        <span className="block text-accanto-700 mb-1">{t('ai.checkInReflection.daysLabel')}</span>
        <input
          type="number"
          min={1}
          max={90}
          value={days}
          onChange={(e) => setDays(Math.max(1, Math.min(90, Number(e.target.value) || 14)))}
          className="input w-24"
        />
      </label>
    </AiAssistPanel>
  );
}
