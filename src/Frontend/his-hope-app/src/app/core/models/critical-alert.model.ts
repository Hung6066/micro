import { CriticalAlertAuditEntry } from './critical-alert-audit.model';

export type CriticalAlertStatus = 'OPEN' | 'ACKNOWLEDGED' | 'RESOLVED';
export type CriticalAlertTriggerType = 'CRITICAL_FLAG' | 'THRESHOLD' | 'BOTH';

export interface CriticalAlert {
  id: string;
  labOrderId: string;
  labTestId: string;
  labResultId: string;
  ruleId?: string | null;
  triggerType: CriticalAlertTriggerType;
  status: CriticalAlertStatus;
  message: string;
  resultValue: string;
  resultUnit?: string | null;
  thresholdValue?: number | null;
  createdAt: string;
  updatedAt: string;
  acknowledgedAt?: string | null;
  acknowledgedByUserId?: string | null;
  acknowledgedByDisplayName?: string | null;
  resolvedAt?: string | null;
  resolvedByUserId?: string | null;
  resolvedByDisplayName?: string | null;
  auditEntries: CriticalAlertAuditEntry[];
}
