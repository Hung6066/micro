export interface MetricDataPoint {
  timestamp: Date;
  value: number;
  labels?: Record<string, string>;
}

export interface MetricSnapshot {
  name: string;
  displayName: string;
  description?: string;
  unit: string;
  currentValue: number;
  previousValue?: number;
  min?: number;
  max?: number;
  avg?: number;
  dataPoints?: MetricDataPoint[];
}
