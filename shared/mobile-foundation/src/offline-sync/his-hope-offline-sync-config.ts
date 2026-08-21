/** Configuration for the secure-storage backed offline sync queue. */
export interface HisHopeOfflineSyncConfig {
  readonly storageKey: string;
  readonly endpoint: string;
}

/** Minimum auth surface the mobile session service needs from the host app. */
export interface HisHopeMobileAuthActions {
  login(): void;
}
