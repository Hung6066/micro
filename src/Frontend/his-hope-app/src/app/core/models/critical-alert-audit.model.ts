export interface CriticalAlertAuditEntry {
  id: string;
  criticalAlertId: string;
  action: 'CREATED' | 'ACKNOWLEDGED' | 'RESOLVED' | 'UPDATED';
  actorUserId: string;
  actorDisplayName: string;
  notes?: string | null;
  occurredAt: string;
}
