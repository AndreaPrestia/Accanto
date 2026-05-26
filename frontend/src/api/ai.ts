import { api } from './client';

export interface AiResponse {
  text: string;
  model: string;
  tookMs: number;
  disclaimer: string;
}

export interface AiStatus {
  available: boolean;
  provider: string;
  model: string;
}

export async function getAiStatus(): Promise<AiStatus> {
  const { data } = await api.get<AiStatus>('/ai/status');
  return data;
}

export async function setCircleAiEnabled(circleId: string, enabled: boolean): Promise<void> {
  await api.put(`/care-circles/${circleId}/ai/settings`, { enabled });
}

export async function timelineSummary(circleId: string, days = 7): Promise<AiResponse> {
  const { data } = await api.post<AiResponse>(`/care-circles/${circleId}/ai/timeline-summary`, { days });
  return data;
}

export async function doctorQuestionDraft(circleId: string, topic: string, notes?: string): Promise<AiResponse> {
  const { data } = await api.post<AiResponse>(`/care-circles/${circleId}/ai/doctor-question-draft`, { topic, notes });
  return data;
}

export async function rephrase(circleId: string, text: string, tone?: string): Promise<AiResponse> {
  const { data } = await api.post<AiResponse>(`/care-circles/${circleId}/ai/rephrase`, { text, tone });
  return data;
}

export async function checkInReflection(days = 14): Promise<AiResponse> {
  const { data } = await api.post<AiResponse>('/me/ai/checkin-reflection', { days });
  return data;
}
