import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

const WELCOME_KEY = 'accanto.hasSeenWelcome';

/**
 * Marca il welcome come visto. Usato da qui e da chi vuole "salta" il flow
 * ovunque compaia in futuro. Silenzioso se localStorage non è disponibile.
 */
export function markWelcomeSeen(): void {
  try {
    localStorage.setItem(WELCOME_KEY, '1');
  } catch {
    /* niente da fare: al prossimo login riappare, pazienza */
  }
}

export function hasSeenWelcome(): boolean {
  try {
    return localStorage.getItem(WELCOME_KEY) === '1';
  } catch {
    return false;
  }
}

/**
 * Onboarding a 3 slide, mostrato dopo la registrazione o al primo accesso
 * su una dashboard vuota. Skippable in qualsiasi momento.
 */
export default function WelcomePage() {
  const { t } = useTranslation();
  const nav = useNavigate();
  const [step, setStep] = useState(0);

  const steps = [
    { title: t('welcome.step1.title'), body: t('welcome.step1.body') },
    { title: t('welcome.step2.title'), body: t('welcome.step2.body') },
    { title: t('welcome.step3.title'), body: t('welcome.step3.body') }
  ];

  const isLast = step === steps.length - 1;
  const current = steps[step];

  const skip = () => {
    markWelcomeSeen();
    nav('/', { replace: true });
  };

  const next = () => {
    if (isLast) {
      markWelcomeSeen();
      const prefill = encodeURIComponent(t('welcome.namePrefill'));
      nav(`/care-circles/new?name=${prefill}`, { replace: true });
      return;
    }
    setStep(step + 1);
  };

  const back = () => {
    if (step > 0) setStep(step - 1);
  };

  return (
    <div className="max-w-md mx-auto pt-4">
      <div className="flex items-center justify-between mb-6">
        <p className="text-sm text-accanto-500">
          {t('welcome.step', { current: step + 1, total: steps.length })}
        </p>
        <button
          type="button"
          onClick={skip}
          className="text-sm text-accanto-500 hover:text-accanto-700 underline"
        >
          {t('welcome.skipCta')}
        </button>
      </div>

      <h1 className="text-2xl font-semibold text-accanto-900 mb-2">
        {t('welcome.title')}
      </h1>
      <p className="text-accanto-500 mb-8">{t('welcome.subtitle')}</p>

      <div
        className="rounded-xl border border-accanto-100 bg-white p-6 min-h-[220px] flex flex-col justify-center"
        role="group"
        aria-live="polite"
      >
        <h2 className="text-lg font-semibold text-accanto-900 mb-2">
          {current.title}
        </h2>
        <p className="text-accanto-700 leading-relaxed">{current.body}</p>
      </div>

      <div className="mt-4 flex items-center justify-center gap-2" aria-hidden="true">
        {steps.map((_, i) => (
          <span
            key={i}
            className={
              'inline-block h-2 rounded-full transition-all ' +
              (i === step ? 'w-6 bg-accanto-700' : 'w-2 bg-accanto-200')
            }
          />
        ))}
      </div>

      <div className="mt-6 flex items-center justify-between gap-3">
        <button
          type="button"
          onClick={back}
          disabled={step === 0}
          className="text-sm text-accanto-700 hover:underline disabled:opacity-40 disabled:no-underline"
        >
          {t('welcome.backCta')}
        </button>
        <button type="button" onClick={next} className="btn-primary">
          {isLast ? t('welcome.ctaCreateFirstCircle') : t('welcome.nextCta')}
        </button>
      </div>
    </div>
  );
}
