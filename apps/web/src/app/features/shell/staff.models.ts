import { ApiEnvelope } from '../cases/models/case.models';

export interface StaffDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  role: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateStaffRequest {
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  phoneNumber?: string;
}

export interface UpdateStaffRequest {
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  role?: string;
}

export const STAFF_ROLES = [
  { value: 'Coordinator', label: 'Coordinator' },
  { value: 'SocialWorker', label: 'Social Worker' },
  { value: 'CaseWorker', label: 'Case Worker' },
] as const;

export type { ApiEnvelope };
