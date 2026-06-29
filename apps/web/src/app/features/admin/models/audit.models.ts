export interface TargetUserSnapshotDto {
  email: string;
  name: string;
  role: string;
}

export interface AuditEventDto {
  id: string;
  eventType: string;
  createdAtUtc: string;
  actorUserId: string | null;
  actorEmail: string | null;
  actorName: string | null;
  subjectUserId: string | null;
  subjectEmail: string | null;
  subjectName: string | null;
  targetUserSnapshot: TargetUserSnapshotDto | null;
  actorIpAddress: string | null;
  metadata: Record<string, unknown> | null;
}

export interface AuditListResultDto {
  items: AuditEventDto[];
}

export interface AuditLogFilter {
  eventType?: string;
  actorUserId?: string;
  subjectUserId?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}
