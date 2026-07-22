export interface LogEntry {
  id: string;
  timestamp: Date;
  level: string;
  service: string;
  category: string;
  message: string;
  exception?: string;
  traceId?: string;
  spanId?: string;
  properties?: Record<string, unknown>;
}
