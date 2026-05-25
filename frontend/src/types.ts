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
export interface AuthResponse { accessToken: string; expiresAt: string; user: User; }
export interface RegisterRequest { email: string; displayName: string; password: string; }
export interface LoginRequest { email: string; password: string; }

export interface CareCircle {
  id: string;
  name: string;
  description?: string | null;
  status: CareCircleStatus;
  myRole: CareCircleRole;
  createdAt: string;
  updatedAt?: string | null;
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

// Italian labels
export const TimelineTypeLabel: Record<TimelineEntryType, string> = {
  MedicalUpdate: 'Aggiornamento medico',
  Symptom: 'Sintomo',
  Medication: 'Farmaco',
  Appointment: 'Appuntamento',
  Decision: 'Decisione',
  PersonalNote: 'Nota personale',
  Practical: 'Pratica',
  Other: 'Altro'
};

export const VisibilityLabel: Record<TimelineVisibility, string> = {
  Circle: 'Tutto il cerchio',
  Private: 'Solo io'
};

export const DocumentCategoryLabel: Record<DocumentCategory, string> = {
  Report: 'Referto',
  BloodTest: 'Esami del sangue',
  Imaging: 'Imaging (radiografia, TAC, RM)',
  Prescription: 'Prescrizione',
  Therapy: 'Terapia',
  IdentityDocument: 'Documento di identità',
  Delegation: 'Delega',
  HospitalContact: 'Contatto ospedaliero',
  Other: 'Altro'
};

export const QuestionCategoryLabel: Record<DoctorQuestionCategory, string> = {
  Diagnosis: 'Diagnosi',
  Therapy: 'Terapia',
  Pain: 'Dolore',
  Nutrition: 'Alimentazione',
  Hydration: 'Idratazione',
  PalliativeCare: 'Cure palliative',
  Discharge: 'Dimissione',
  HomeCare: 'Assistenza domiciliare',
  Emergency: 'Emergenza',
  Prognosis: 'Prognosi',
  Practical: 'Pratica',
  Other: 'Altro'
};

export const QuestionStatusLabel: Record<DoctorQuestionStatus, string> = {
  ToAsk: 'Da chiedere',
  Asked: 'Chiesta',
  Answered: 'Risposta ricevuta',
  Archived: 'Archiviata'
};

export const AudienceLabel: Record<SharedUpdateAudience, string> = {
  CloseFamily: 'Famiglia stretta',
  ExtendedFamily: 'Famiglia allargata',
  Friends: 'Amici',
  Generic: 'Messaggio generico'
};

export const RoleLabel: Record<CareCircleRole, string> = {
  Owner: 'Coordinatore',
  Caregiver: 'Caregiver',
  Viewer: 'In ascolto'
};
