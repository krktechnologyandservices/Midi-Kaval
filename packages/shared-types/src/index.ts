export enum AppRole {
  Director = 'Director',
  Coordinator = 'Coordinator',
  Accountant = 'Accountant',
  SocialWorker = 'SocialWorker',
  CaseWorker = 'CaseWorker',
  Vendor = 'Vendor',
}

export enum SyncState {
  Local = 'local',
  Pending = 'pending',
  Synced = 'synced',
  Error = 'error',
}

export enum InvitationStatus {
  Pending = 'pending',
  Confirmed = 'confirmed',
  Expired = 'expired',
}

export enum ConfirmationStatus {
  Pending = 'pending',
  Confirmed = 'confirmed',
  Expired = 'expired',
}
