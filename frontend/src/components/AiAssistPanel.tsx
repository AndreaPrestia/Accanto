import { ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AxiosError } from 'axios';
import { AiResponse } from '../api/ai';
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

  const handleClick = async () => {
    setBusy(true);
    setError(null);
    setResult(null);
    setCopied(false);
    try {
      const r = await onGenerate();
      setResult(r);
    } catch (e) {
      const ax = e as AxiosError<{ title?: string; detail?: string }>;
      const status = ax?.response?.status;
      const msg = ax?.response?.data?.title || ax?.response?.data?.detail || '';
      if (status === 503) {
        setError(/disabled|disattiv/i.test(msg) ? t('ai.errors.disabledForCircle') : t('ai.errors.notConfigured'));
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
            </div>
          )}
        </>
      )}
    </section>
  );
}
