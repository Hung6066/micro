export interface ServicePlugin {
  key: string;
  displayName: string;
  dashboardRoute: string | null;
  icon: string | null;
  permissions: string[];
}
