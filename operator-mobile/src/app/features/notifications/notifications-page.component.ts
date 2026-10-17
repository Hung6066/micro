import { ChangeDetectorRef, Component, inject } from "@angular/core";
import { DatePipe } from "@angular/common";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { MobileAdminApiService, type MobileNotification } from "../../core/admin-api.service";

@Component({ standalone: true, imports: [DatePipe, HisHopeTranslatePipe], templateUrl: "./notifications-page.component.html", styleUrls: ["./notifications-page.component.scss"] })
export class NotificationsPageComponent {
  private readonly api = inject(MobileAdminApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  notifications: MobileNotification[] = [];
  unread = 0;
  loading = false;
  error = "";

  constructor() { void this.load(); }

  async load(): Promise<void> {
    this.loading = true;
    this.error = "";
    try {
      const page = await this.api.list(1, 30);
      this.notifications = [...page.items];
      this.unread = page.unread;
    } catch (error) {
      this.error = this.i18n.t("mobile.operatorNotificationsError", "Notifications could not be loaded.");
      void error;
    } finally {
      this.loading = false;
      this.cdr.markForCheck();
    }
  }

  async markRead(notification: MobileNotification): Promise<void> {
    if (notification.readAt) return;
    await this.api.markRead(notification.id).catch(() => undefined);
    this.notifications = this.notifications.map((item) => item.id === notification.id ? { ...item, readAt: new Date().toISOString() } : item);
    this.unread = Math.max(0, this.unread - 1);
  }

  async markAllRead(): Promise<void> {
    await this.api.markAllRead().catch(() => undefined);
    const now = new Date().toISOString();
    this.notifications = this.notifications.map((item) => item.readAt ? item : { ...item, readAt: now });
    this.unread = 0;
  }
}
