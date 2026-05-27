import { ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AxiosError } from 'axios';
import { AiResponse, submitAiFeedback, AiFeedback } from '../api/ai';
import { extractError } from '../api/client';

interface Props {
  title: string;
  description?: string;
  ctaLabel: string;
  onGenerate: () => Promise<AiResponse>;
  children?: ReactNode;
  disabled?: boolean;
  disabledReason?: string;
}

export default function AiAssistPanel({ title, description, ctaLabel, onGenerate, children, disabled, disabledReason }: Props) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<AiResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [feedback, setFeedback] = useState<AiFeedback | null>(null);
  const [feedbackBusy, setFeedbackBusy] = useState(false);

  const handleClick = async () => {
    setBusy(true);
    setError(null);
    setResult(null);
    setCopied(false);
    setFeedback(null);
    try {
      const r = await onGenerate();
      setResult(r);
    } catch (e) {
      const ax = e as AxiosError<{ title?: string; detail?: string }>;
      const status = ax?.response?.status;
      const msg = ax?.response?.data?.title || ax?.response?.data?.detail || '';
      if (status === 503) {
        setError(/disabled|disattiv/i.test(msg) ? t('ai.errors.disabledForCircle') : t('ai.errors.notConfigured'));
      } else if (status === 422 && /ai_input_rejected/i.test(msg)) {
        setError(t('ai.errors.inputRejected') as string);
      } else {
        setError(extractError(e) || (t('ai.errors.generic') as string));
      }
    } finally {
      setBusy(false);
    }
  };

  const copy = async () => {
    if (!result) return;
    try {
      await navigator.clipboard.writeText(result.text);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      // ignore
    }
  };

  const sendFeedback = async (value: AiFeedback) => {
    if (!result?.interactionId || feedbackBusy) return;
    setFeedbackBusy(true);
    try {
      await submitAiFeedback(result.interactionId, value);
      setFeedback(value);
    } catch {
      // ignore feedback errors – non-blocking
    } finally {
      setFeedbackBusy(false);
    }
  };

  return (
    <section className="card mt-4">
      <h3 className="font-medium">{title}</h3>
      {description && <p className="text-sm text-accanto-500 mt-1">{description}</p>}
      {disabled ? (
        <p className="text-sm text-accanto-500 mt-3">{disabledReason}</p>
      ) : (
        <>
          {children && <div className="mt-3 space-y-2">{children}</div>}
          <div className="mt-3">
            <button type="button" className="btn-primary" onClick={handleClick} disabled={busy}>
              {busy ? t('ai.generating') : ctaLabel}
            </button>
          </div>
          {error && (
            <div className="text-sm text-red-700 bg-red-50 border border-red-200 rounded-md px-3 py-2 mt-3">{error}</div>
          )}
          {result && (
            <div className="mt-4 rounded-md border border-accanto-200 bg-accanto-50 px-3 py-3">
              <div className="flex items-center justify-between gap-3">
                <span className="text-xs uppercase tracking-wide text-accanto-500">{t('ai.result')}</span>
                <button type="button" className="text-xs text-accanto-600 hover:underline" onClick={copy}>
                  {copied ? t('ai.copied') : t('ai.copy')}
                </button>
              </div>
              <p className="whitespace-pre-wrap text-sm mt-2">{result.text}</p>
              <p className="text-xs text-accanto-500 mt-3 italic">
                {result.disclaimer || t('ai.disclaimer')}
              </p>
              {result.interactionId && (
                <div className="flex items-center gap-2 mt-3 pt-3 border-t border-accanto-200">
                  <span className="text-xs text-accanto-500">{t('ai.feedback.label')}</span>
                  <button type="button"
                    className={`text-xs px-2 py-1 rounded ${feedback === 'Up' ? 'bg-green-100 text-green-700' : 'text-accanto-600 hover:bg-accanto-100'}`}
                    onClick={() => sendFeedback('Up')}
                    disabled={feedbackBusy || !!feedback}
                    aria-label={t('ai.feedback.up') as string}>👍</button>
                  <button type="button"
                    className={`text-xs px-2 py-1 rounded ${feedback === 'Down' ? 'bg-red-100 text-red-700' : 'text-accanto-600 hover:bg-accanto-100'}`}
                    onClick={() => sendFeedback('Down')}
                    disabled={feedbackBusy || !!feedback}
                    aria-label={t('ai.feedback.down') as string}>👎</button>
                  <button type="button"
                    className={`text-xs px-2 py-1 rounded ${feedback === 'Flag' ? 'bg-amber-100 text-amber-700' : 'text-accanto-600 hover:bg-accanto-100'}`}
                    onClick={() => sendFeedback('Flag')}
                    disabled={feedbackBusy || !!feedback}
                    aria-label={t('ai.feedback.flag') as string}>🚩</button>
                  {feedback && (
                    <span className="text-xs text-accanto-500 ml-1">{t('ai.feedback.thanks')}</span>
                  )}
                  {result.cacheHit && (
                    <span className="ml-auto text-[10px] uppercase tracking-wide text-accanto-400" title={t('ai.cacheHitTitle') as string}>
                      {t('ai.cacheHit')}
                    </span>
                  )}
                </div>
              )}
            </div>
          )}
        </>
      )}
    </section>
  );
}
