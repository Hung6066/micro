export interface TraceSpan {
  spanId: string;
  parentSpanId?: string;
  name: string;
  service: string;
  startTime: Date;
  endTime: Date;
  durationMs: number;
  status: string;
  attributes?: Record<string, string>;
  events?: TraceSpanEvent[];
}

export interface TraceSpanEvent {
  name: string;
  timestamp: Date;
  attributes?: Record<string, string>;
}

export interface TraceSummary {
  traceId: string;
  rootService: string;
  rootName: string;
  startTime: Date;
  durationMs: number;
  spanCount: number;
  status: string;
  hasErrors: boolean;
}

export interface TraceDetail {
  traceId: string;
  rootService: string;
  rootName: string;
  startTime: Date;
  endTime: Date;
  durationMs: number;
  spanCount: number;
  status: string;
  spans: TraceSpan[];
  services: string[];
}
