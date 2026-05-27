import { api } from './client';

export interface AiResponse {
  text: string;
  model: string;
  tookMs: number;
  disclaimer: string;
  interactionId: string;
  verdict: string;
  cacheHit: boolean;
}

export interface AiStatus {
  available: boolean;
  provider: string;
  model: string;
}

export type AiFeedback = 'Up' | 'Down' | 'Flag';

export interface AiInteractionSummary {
  id: string;
  userId: string;
  careCircleId?: string | null;
  function: string;
  verdict: string;
  feedback?: string | null;
  model: string;
  language: string;
  tookMs: number;
  createdAt: string;
}

export interface AiInteractionDetail extends AiInteractionSummary {
  promptVersion: string;
  cacheHit: boolean;
  input: string;
  output: string;
}

export interface AiInteractionListResponse {
  items: AiInteractionSummary[];
  page: number;
  pageSize: number;
  total: number;
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

export async function listAiInteractions(params: {
  circleId?: string;
  function?: string;
  page?: number;
  pageSize?: number;
} = {}): Promise<AiInteractionListResponse> {
  const { data } = await api.get<AiInteractionListResponse>('/ai/interactions', { params });
  return data;
}

export async function getAiInteraction(id: string): Promise<AiInteractionDetail> {
  const { data } = await api.get<AiInteractionDetail>(`/ai/interactions/${id}`);
  return data;
}

export async function submitAiFeedback(id: string, value: AiFeedback): Promise<void> {
  await api.post(`/ai/interactions/${id}/feedback`, { value });
}
