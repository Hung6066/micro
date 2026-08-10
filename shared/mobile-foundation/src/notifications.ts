import type { HisHopeMobilePlatform, HisHopePushCapability } from "./index";

export interface HisHopeNotification {
  readonly id: string;
  readonly title: string;
  readonly body: string;
  readonly dataJson?: string;
  readonly createdAt: string;
  readonly readAt?: string;
}

export interface HisHopeNotificationPage {
  readonly items: readonly HisHopeNotification[];
  readonly page: number;
  readonly pageSize: number;
  readonly total: number;
  readonly unread: number;
}

export interface HisHopeNotificationInboxApi {
  list(page?: number, pageSize?: number): Promise<HisHopeNotificationPage>;
  markRead(id: string): Promise<void>;
  markAllRead(): Promise<{ updated: number }>;
}

export interface HisHopePushTokenRegistrar {
  registerToken(token: string, platform: HisHopeMobilePlatform): Promise<void>;
}

/** Deep orchestration seam shared by native mobile applications. */
export class HisHopePushRegistrationCoordinator {
  constructor(
    private readonly capability: HisHopePushCapability,
    private readonly registrar: HisHopePushTokenRegistrar,
    private readonly platform: Exclude<HisHopeMobilePlatform, "web">,
  ) {}

  async register(): Promise<string | null> {
    const token = await this.capability.register();
    if (token) await this.registrar.registerToken(token, this.platform);
    return token;
  }

  unregister(): Promise<void> {
    return this.capability.unregister();
  }
}
