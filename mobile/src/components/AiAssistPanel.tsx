import { ReactNode, useState } from 'react';
import {
  Modal,
  Pressable,
  Text,
  TouchableOpacity,
  View
} from 'react-native';
import * as Clipboard from 'expo-clipboard';
import { useTranslation } from 'react-i18next';
import { AxiosError } from 'axios';
import Button from './ui/Button';
import ErrorBanner from './ui/ErrorBanner';
import {
  submitAiFeedback,
  type AiFeedback,
  type AiResponse
} from '../api/ai';
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

/**
 * Pannello generico per le funzioni assistive AI: bottone genera, output
 * con disclaimer, copia, feedback (Up/Down/Flag). Le altre schermate (Timeline,
 * SelfCare, DoctorQuestions, SharedUpdates) passano `onGenerate` con la
 * funzione API specifica e i campi di input come children.
 */
export default function AiAssistPanel({
  title,
  description,
  ctaLabel,
  onGenerate,
  children,
  disabled,
  disabledReason
}: Props) {
  const { t } = useTranslation();
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<AiResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [feedback, setFeedback] = useState<AiFeedback | null>(null);
  const [feedbackBusy, setFeedbackBusy] = useState(false);

  const handleGenerate = async () => {
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
        setError(
          /disabled|disattiv/i.test(msg)
            ? (t('ai.errors.disabledForCircle') as string)
            : (t('ai.errors.notConfigured') as string)
        );
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
    await Clipboard.setStringAsync(result.text);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  const sendFeedback = async (value: AiFeedback) => {
    if (!result?.interactionId || feedbackBusy) return;
    setFeedbackBusy(true);
    try {
      await submitAiFeedback(result.interactionId, value);
      setFeedback(value);
    } catch {
      // feedback non-blocking
    } finally {
      setFeedbackBusy(false);
    }
  };

  return (
    <View className="rounded-lg border border-accanto-100 bg-white p-4 gap-2">
      <Text className="font-medium text-accanto-900">{title}</Text>
      {description ? (
        <Text className="text-sm text-accanto-500">{description}</Text>
      ) : null}

      {disabled ? (
        <Text className="text-sm text-accanto-500 mt-1">{disabledReason}</Text>
      ) : (
        <>
          {children ? <View className="gap-2 mt-2">{children}</View> : null}
          <View className="mt-2">
            <Button onPress={handleGenerate} busy={busy} disabled={busy}>
              {busy ? (t('ai.generating') as string) : ctaLabel}
            </Button>
          </View>
          <ErrorBanner message={error} />
          {result ? (
            <View className="mt-3 rounded-md border border-accanto-100 bg-accanto-50 px-3 py-3">
              <View className="flex-row items-center justify-between">
                <Text className="text-xs uppercase tracking-wide text-accanto-500">
                  {t('ai.result') as string}
                </Text>
                <Pressable onPress={copy}>
                  <Text className="text-xs text-accanto-700 underline">
                    {copied
                      ? (t('ai.copied') as string)
                      : (t('ai.copy') as string)}
                  </Text>
                </Pressable>
              </View>
              <Text className="text-sm text-accanto-900 mt-2">
                {result.text}
              </Text>
              <Text className="text-xs text-accanto-500 mt-3 italic">
                {result.disclaimer || (t('ai.disclaimer') as string)}
              </Text>
              {result.interactionId ? (
                <View className="flex-row items-center gap-2 mt-3 pt-3 border-t border-accanto-100 flex-wrap">
                  <Text className="text-xs text-accanto-500">
                    {t('ai.feedback.label') as string}
                  </Text>
                  <FeedbackButton
                    label="👍"
                    active={feedback === 'Up'}
                    disabled={feedbackBusy || !!feedback}
                    onPress={() => sendFeedback('Up')}
                    accessibilityLabel={t('ai.feedback.up') as string}
                  />
                  <FeedbackButton
                    label="👎"
                    active={feedback === 'Down'}
                    disabled={feedbackBusy || !!feedback}
                    onPress={() => sendFeedback('Down')}
                    accessibilityLabel={t('ai.feedback.down') as string}
                  />
                  <FeedbackButton
                    label="🚩"
                    active={feedback === 'Flag'}
                    disabled={feedbackBusy || !!feedback}
                    onPress={() => sendFeedback('Flag')}
                    accessibilityLabel={t('ai.feedback.flag') as string}
                  />
                  {feedback ? (
                    <Text className="text-xs text-accanto-500 ml-1">
                      {t('ai.feedback.thanks') as string}
                    </Text>
                  ) : null}
                  {result.cacheHit ? (
                    <Text className="text-xs text-accanto-500 ml-auto uppercase">
                      {t('ai.cacheHit') as string}
                    </Text>
                  ) : null}
                </View>
              ) : null}
            </View>
          ) : null}
        </>
      )}
    </View>
  );
}

function FeedbackButton({
  label,
  active,
  disabled,
  onPress,
  accessibilityLabel
}: {
  label: string;
  active: boolean;
  disabled: boolean;
  onPress: () => void;
  accessibilityLabel: string;
}) {
  return (
    <TouchableOpacity
      onPress={onPress}
      disabled={disabled}
      accessibilityLabel={accessibilityLabel}
      accessibilityRole="button"
      className={`px-2 py-1 rounded ${
        active ? 'bg-accanto-100' : 'bg-transparent'
      } ${disabled && !active ? 'opacity-50' : ''}`}
    >
      <Text className="text-base">{label}</Text>
    </TouchableOpacity>
  );
}

/**
 * Versione modale per usi compatti (es. dentro Timeline bulk select):
 * apre AiAssistPanel in un overlay full-screen.
 */
export function AiAssistModal({
  visible,
  onClose,
  ...panelProps
}: Props & { visible: boolean; onClose: () => void }) {
  return (
    <Modal
      visible={visible}
      transparent
      animationType="slide"
      onRequestClose={onClose}
    >
      <View className="flex-1 bg-black/40 justify-end">
        <View className="bg-white rounded-t-2xl p-4 max-h-[85%]">
          <View className="flex-row items-center justify-end mb-2">
            <Pressable onPress={onClose} className="p-2">
              <Text className="text-accanto-500 text-lg">×</Text>
            </Pressable>
          </View>
          <AiAssistPanel {...panelProps} />
        </View>
      </View>
    </Modal>
  );
}
