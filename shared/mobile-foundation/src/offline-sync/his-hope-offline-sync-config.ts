/** Configuration for the secure-storage backed offline sync queue. */
export interface HisHopeOfflineSyncConfig {
  readonly storageKey: string;
  readonly endpoint: string;
}

/** Minimum auth surface mobile shell security needs from the host app. */
export interface HisHopeMobileAuthActions {
  login(): void;
  logout?(): void;
}
