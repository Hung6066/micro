import { MetricDataPoint } from './metric-snapshot.model';

/** SLO/SLI record for a single service. */
export interface SloRecord {
  service: string;
  displayName: string;
  availability: number;
  errorBudgetRemaining: number;
  burnRate1h: number;
  burnRate6h: number;
  latencyP99: number;
}

/** Envelope returned by GET /api/slo. */
export interface SloResponse {
  services: SloRecord[];
  sparklineData?: MetricDataPoint[];
}
