// Tipi del control plane admin. Rispecchiano i DTO dell'Admin API.
// SOLO metadata tecnici: nessun contenuto utente (nomi cerchi, titoli/
// contenuti timeline, filename, path, domande, aggiornamenti).

export interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface AdminAuthResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  adminUser: AdminUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface UserMetadata {
  userId: string;
  email: string;
  displayName: string;
  createdAt: string;
  isDisabled: boolean;
  accountStatus: string;
  disabledAt: string | null;
  disabledReason: string | null;
  careCircleCount: number;
  documentsCount: number;
  storageUsedBytes: number;
  timelineEntryCount: number;
}

export interface UserListResponse {
  items: UserMetadata[];
  page: number;
  pageSize: number;
  total: number;
}

export interface OperationResult {
  operationId: string;
  status: string;
}

export interface Operation {
  id: string;
  requestedByAdminUserId: string;
  operationType: string;
  targetUserId: string | null;
  status: string;
  reason: string;
  createdAt: string;
  completedAt: string | null;
  errorMessage: string | null;
}

export interface OperationListResponse {
  items: Operation[];
  page: number;
  pageSize: number;
  total: number;
}

export interface AuditLogEntry {
  id: string;
  adminUserId: string;
  adminEmail: string | null;
  action: string;
  targetType: string;
  targetId: string | null;
  reason: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
}

export interface AuditLogListResponse {
  items: AuditLogEntry[];
  page: number;
  pageSize: number;
  total: number;
}

export interface SystemHealth {
  adminApi: string;
  adminDb: string;
  publicApiInternal: string;
  checkedAt: string;
}

export interface AdminStats {
  totalUsers: number;
  disabledUsers: number;
  totalStorageBytes: number;
  totalDocuments: number;
  totalTimelineEntries: number;
}
