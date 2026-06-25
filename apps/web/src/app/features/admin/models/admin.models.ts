export interface AdminUserSummary {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  isActive: boolean;
  isSuspended: boolean;
  createdAtUtc: string;
}

export interface AdminUserListResult {
  items: AdminUserSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SendInvitationRequest {
  email: string;
  role: string;
  message?: string;
}

export interface InvitationSummary {
  id: string;
  targetEmail: string;
  role: string;
  status: 'pending' | 'confirmed' | 'expired';
  createdAtUtc: string;
  expiresAtUtc: string;
  confirmedAtUtc?: string;
}

export interface InvitationListResult {
  items: InvitationSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SendInvitationResponse {
  id: string;
  targetEmail: string;
  role: string;
  message: string;
}

export interface ResendInvitationResponse {
  id: string;
  targetEmail: string;
  newExpiresAtUtc: string;
  message: string;
}
