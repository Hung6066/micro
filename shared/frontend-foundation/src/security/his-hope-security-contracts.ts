export type HisHopeExternalProviderKind = "oidc" | "saml" | "ldap";

export interface HisHopeExternalProvider {
  readonly kind: HisHopeExternalProviderKind;
  readonly displayName: string;
  readonly enabled: boolean;
  readonly loginUrl?: string;
}

export interface HisHopePasskeyStatus {
  readonly registered: boolean;
  readonly count?: number;
  readonly createdAt?: string;
}

export interface HisHopeMfaStatus {
  readonly enabled: boolean;
  readonly enrolledAt?: string;
  readonly recoveryCodesRemaining: number;
}

export interface HisHopeIdentitySecurityClient {
  passkeyStatus(): Promise<HisHopePasskeyStatus>;
  mfaStatus(): Promise<HisHopeMfaStatus>;
}

