import type {
  HisHopePageQuery,
  HisHopePageResult,
  HisHopeBulkActionResult,
} from "@his-hope/frontend-foundation/contracts";

export interface PermissionDefinition {
  code: string;
  name: string;
  description?: string;
  group: string;
  isDeprecated?: boolean;
}

export interface RoleOwnerOption {
  key: string;
  name: string;
}

export interface ClientSecretResponse {
  clientId: string;
  clientSecret: string;
  message: string;
  tokenEndpointAuthMethod?: string;
}

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  role?: string;
}

export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  email?: string;
  phoneNumber?: string;
  role?: string;
  isActive?: boolean;
  concurrencyToken?: string;
}

export interface OidcClient {
  id?: string;
  clientId: string;
  displayName: string;
  clientType: string;
  redirectUris: string[];
  postLogoutRedirectUris?: string[];
  permissions?: string[];
  scopes?: string[];
  grantTypes?: string[];
  jwks?: string;
  concurrencyToken?: string;
}

export interface User {
  id: string;
  userName: string;
  email: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  roles: string[];
  isActive: boolean;
  concurrencyToken?: string;
}

export interface Role {
  id?: string;
  name: string;
  description?: string;
  concurrencyToken?: string;
  owner?: string;
  riskTier?: string;
  permissions?: string[] | PermissionDefinition[];
}

export interface Consent {
  id: string;
  subject: string;
  clientId: string;
  scopes: string[];
  created: string;
}

export interface DashboardStats {
  totalClients: number;
  totalUsers: number;
  totalRoles: number;
  totalConsents: number;
}

export type MobilePageQuery = HisHopePageQuery;
export type MobilePageResult<T> = HisHopePageResult<T>;
export type MobileBulkActionResponse = HisHopeBulkActionResult;

export type MobileResource = "clients" | "users" | "roles" | "consents";
export type MobileTableResource = Exclude<MobileResource, "consents">;

/** @deprecated Use OidcClient */
export type MobileClient = OidcClient;
/** @deprecated Use User */
export type MobileUser = User;
/** @deprecated Use Role */
export type MobileRole = Role;
/** @deprecated Use Consent */
export type MobileConsent = Consent;
/** @deprecated Use DashboardStats */
export type MobileDashboardStats = DashboardStats;
