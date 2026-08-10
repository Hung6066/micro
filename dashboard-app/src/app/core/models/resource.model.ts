export interface HealthCheckResult {
  status: string;
  description: string;
  duration: string;
  tags?: Record<string, string>;
  data?: Record<string, unknown>;
}

export interface Resource {
  name: string;
  displayName: string;
  status: string;
  healthStatus: string;
  type: string;
  version?: string;
  description?: string;
  tags?: Record<string, string>;
  healthChecks?: HealthCheckResult[];
  lastUpdated?: Date;
  cpuPercent?: number;
  memoryUsedMb?: number;
}

export interface ServiceResource extends Resource {
  serviceType: string;
  instanceCount: number;
  cpuPercent?: number;
  memoryUsedMb?: number;
  httpPort?: number;
  grpcPort?: number;
  uptime?: string;
  databases?: string[];
  baseUrl?: string;
  isExternal: boolean;
}

export interface DatabaseResource extends Resource {
  databaseType: string;
  host?: string;
  port?: number;
  databaseName?: string;
  connectionState?: string;
  migrationStatus?: string;
}

export interface InfrastructureResource extends Resource {
  infrastructureType: string;
  provider?: string;
  region?: string;
  endpoints?: string[];
}
