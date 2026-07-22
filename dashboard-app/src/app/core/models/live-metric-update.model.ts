export interface LiveMetricUpdate {
  serviceName: string;
  cpu: number;
  memory: number;
  requests: number;
  timestamp: Date;
}
