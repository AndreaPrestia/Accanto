export type CareCircleRole = 'Owner' | 'Caregiver' | 'Viewer';
export type CareCircleStatus = 'Active' | 'Archived';

export type TimelineEntryType =
  | 'MedicalUpdate' | 'Symptom' | 'Medication' | 'Appointment'
  | 'Decision' | 'PersonalNote' | 'Practical' | 'Other';

export type TimelineVisibility = 'Circle' | 'Private';

export type DocumentCategory =
  | 'Report' | 'BloodTest' | 'Imaging' | 'Prescription' | 'Therapy'
  | 'IdentityDocument' | 'Delegation' | 'HospitalContact' | 'Other';

export type DoctorQuestionCategory =
  | 'Diagnosis' | 'Therapy' | 'Pain' | 'Nutrition' | 'Hydration'
  | 'PalliativeCare' | 'Discharge' | 'HomeCare' | 'Emergency'
  | 'Prognosis' | 'Practical' | 'Other';

export type DoctorQuestionStatus = 'ToAsk' | 'Asked' | 'Answered' | 'Archived';

export type SharedUpdateAudience = 'CloseFamily' | 'ExtendedFamily' | 'Friends' | 'Generic';

export interface User { id: string; email: string; displayName: string; language?: string | null; createdAt: string; }
export interface AuthResponse { accessToken: string; expiresAt: string; refreshToken: string; refreshTokenExpiresAt: string; user: User; }
export interface RegisterRequest { email: string; displayName: string; password: string; }
export interface LoginRequest { email: string; password: string; }
export interface LoginResult {
  requiresTwoFactor: boolean;
  twoFactorToken?: string | null;
  twoFactorTokenExpiresAt?: string | null;
  auth?: AuthResponse | null;
}
export interface TwoFactorStatus { enabled: boolean; remainingRecoveryCodes: number; }
export interface TwoFactorSetupResponse { secret: string; otpAuthUri: string; }
export interface TwoFactorEnableResponse { recoveryCodes: string[]; }
export interface ActiveSession {
  id: string;
  createdAt: string;
  expiresAt: string;
  userAgent?: string | null;
  ipAddress?: string | null;
  current: boolean;
}

export type SecurityAuditEventType =
  | 'AccountRegistered'
  | 'LoginSuccess' | 'LoginFailed' | 'LoginLocked'
  | 'TwoFactorChallengeIssued' | 'TwoFactorSuccess' | 'TwoFactorFailed'
  | 'TwoFactorEnabled' | 'TwoFactorDisabled'
  | 'RecoveryCodeUsed' | 'RecoveryCodesRegenerated'
  | 'PasswordChanged' | 'SessionRevoked' | 'AllSessionsRevoked';

export interface SecurityAuditEntry {
  id: string;
  userId?: string | null;
  eventType: SecurityAuditEventType;
  summary?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  timestamp: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  skip: number;
  take: number;
}

export interface CaregiverCheckIn {
  id: string;
  mood: number;
  energy: number;
  stress: number;
  note?: string | null;
  createdAt: string;
}

export interface CareCircle {
  id: string;
  name: string;
  description?: string | null;
  status: CareCircleStatus;
  myRole: CareCircleRole;
  createdAt: string;
  updatedAt?: string | null;
  aiEnabled: boolean;
}

export interface TimelineEntry {
  id: string;
  careCircleId: string;
  createdByUserId: string;
  occurredAt: string;
  type: TimelineEntryType;
  title: string;
  content: string;
  tags: string[];
  visibility: TimelineVisibility;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DocumentItem {
  id: string;
  careCircleId: string;
  uploadedByUserId: string;
  originalFileName: string;
  contentType: string;
  sizeInBytes: number;
  category: DocumentCategory;
  notes?: string | null;
  tags: string[];
  createdAt: string;
}

export interface DoctorQuestion {
  id: string;
  careCircleId: string;
  createdByUserId: string;
  question: string;
  category: DoctorQuestionCategory;
  status: DoctorQuestionStatus;
  answerNotes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface DoctorQuestionTemplate {
  category: DoctorQuestionCategory;
  categoryLabel: string;
  questions: string[];
}

export interface SharedUpdate {
  id: string;
  careCircleId: string;
  createdByUserId: string;
  audience: SharedUpdateAudience;
  content: string;
  createdAt: string;
}

export interface SharedUpdateTemplate { title: string; content: string; }

export interface CareCircleInvite {
  id: string;
  careCircleId: string;
  role: CareCircleRole;
  token: string;
  expiresAt: string;
  maxUses: number;
  usedCount: number;
  revokedAt?: string | null;
  createdAt: string;
  isActive: boolean;
}

export interface CareCircleInvitePreview {
  circleName: string;
  role: CareCircleRole;
  expiresAt: string;
  invitedByDisplayName: string;
}

export interface CreateInviteRequest {
  role: CareCircleRole;
  expiresInDays?: number | null;
  maxUses?: number | null;
}

// User-facing labels for these enums now live in the i18n locale files
// (timelineType.*, visibility.*, documentCategory.*, questionCategory.*,
// questionStatus.*, audience.*, roles.*) and are resolved via t() in each app.
