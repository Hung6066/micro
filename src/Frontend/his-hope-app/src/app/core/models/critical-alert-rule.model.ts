export interface CriticalAlertRule {
  id?: string;
  testCode: string;
  testName: string;
  unit?: string | null;
  lowCriticalValue?: number | null;
  highCriticalValue?: number | null;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string | null;
  createdByUserId?: string;
  createdByDisplayName?: string;
}

export interface CriticalAlertRuleRequest {
  id?: string;
  testCode: string;
  testName: string;
  unit?: string | null;
  lowCriticalValue?: number | null;
  highCriticalValue?: number | null;
  isActive: boolean;
}
