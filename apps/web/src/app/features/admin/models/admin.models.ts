export interface AdminUserSummary {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  isActive: boolean;
  isSuspended: boolean;
  suspendedAtUtc?: string | null;
  createdAtUtc: string;
  totpEnrolledAt?: string | null;
}

export interface AdminUserListResult {
  items: AdminUserSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type UserDisplayStatus = 'active' | 'suspended' | 'deleted';

export function getUserStatus(user: AdminUserSummary | null | undefined): UserDisplayStatus {
  if (!user) return 'active';
  if (!user.isActive && user.email.startsWith('deleted-')) return 'deleted';
  if (user.isSuspended) return 'suspended';
  return 'active';
}

export function getUserStatusLabel(status: UserDisplayStatus): string {
  switch (status) {
    case 'active': return 'Active';
    case 'suspended': return 'Suspended';
    case 'deleted': return 'Deleted';
  }
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
  invitedByUserEmail: string;
  invitedByUserName?: string;
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

export interface SuspendUserRequest {
  reason?: string;
}

export interface UserActionResponse {
  id: string;
  isSuspended: boolean;
  actionedAtUtc: string;
  message: string;
}

export interface DeleteUserRequest {
  confirmationEmail: string;
}

export interface DeleteUserResponse {
  id: string;
  deletedAtUtc: string;
  message: string;
}
