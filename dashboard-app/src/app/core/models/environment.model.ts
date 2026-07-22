export interface EnvironmentContext {
  name: string;
  displayName: string;
  version: string;
  machineName: string;
  os: string;
  processId: number;
  architecture: string;
  environmentVariables?: Record<string, string>;
  assemblyVersions?: Record<string, string>;
  isDevelopment: boolean;
  isProduction: boolean;
  startupTime: Date;
  uptime: string;
}
