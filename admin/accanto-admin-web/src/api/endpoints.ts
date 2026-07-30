import { adminApi } from './client';
import {
  AuditLogListResponse,
  Operation,
  OperationListResponse,
  OperationResult,
  SystemHealth,
  UserListResponse,
  UserMetadata
} from '../types';

// --- Users (metadata only) ---
export async function listUsers(params: {
  q?: string;
  disabled?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<UserListResponse> {
  const { data } = await adminApi.get<UserListResponse>('/api/admin/users', { params });
  return data;
}

export async function getUser(userId: string): Promise<UserMetadata> {
  const { data } = await adminApi.get<UserMetadata>(`/api/admin/users/${userId}`);
  return data;
}

async function userOperation(userId: string, op: string, reason: string): Promise<OperationResult> {
  const { data } = await adminApi.post<OperationResult>(`/api/admin/users/${userId}/${op}`, { reason });
  return data;
}

export const disableUser = (userId: string, reason: string) => userOperation(userId, 'disable', reason);
export const enableUser = (userId: string, reason: string) => userOperation(userId, 'enable', reason);
export const revokeUserSessions = (userId: string, reason: string) => userOperation(userId, 'revoke-sessions', reason);
export const startUserDeletion = (userId: string, reason: string) => userOperation(userId, 'deletion-requests', reason);

// --- Audit logs ---
export async function listAuditLogs(params: {
  adminUserId?: string;
  action?: string;
  targetType?: string;
  targetId?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}): Promise<AuditLogListResponse> {
  const { data } = await adminApi.get<AuditLogListResponse>('/api/admin/audit-logs', { params });
  return data;
}

// --- Operations ---
export async function listOperations(params: { page?: number; pageSize?: number }): Promise<OperationListResponse> {
  const { data } = await adminApi.get<OperationListResponse>('/api/admin/operations', { params });
  return data;
}

export async function getOperation(operationId: string): Promise<Operation> {
  const { data } = await adminApi.get<Operation>(`/api/admin/operations/${operationId}`);
  return data;
}

// --- System ---
export async function getSystemHealth(): Promise<SystemHealth> {
  const { data } = await adminApi.get<SystemHealth>('/api/admin/system/health');
  return data;
}
