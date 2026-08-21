import { ChangeDetectorRef, Component, OnInit, inject } from "@angular/core";
import { catchError, finalize, of } from "rxjs";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeMobileIconComponent,
  HisHopeStateComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  MobileAdminApiService,
  MobileNotification,
} from "../core/admin-api.service";

@Component({
  standalone: true,
  imports: [
    HisHopeMobileIconComponent,
    HisHopeStateComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <section class="mobile-page">
      <hh-toolbar [label]="'mobile.notifications' | hhTranslate"
        ><span hhToolbarTitle>{{ "mobile.notifications" | hhTranslate }}</span
        ><button
          hh-toolbar-actions
          class="hh-button hh-button--secondary"
          type="button"
          (click)="markAllRead()"
          [disabled]="!unread"
        >
          {{ "mobile.markAllRead" | hhTranslate }}
        </button></hh-toolbar
      >
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="'mobile.notifications' | hhTranslate"
        />
      } @else if (error) {
        <hh-state kind="error" [message]="error" />
      } @else {
        <p class="unread">
          {{ i18n.t('mobile.unreadNotifications', '{{count}} unread', { count:
          unread }) }}
        </p>
        @if (!items.length) {
          <hh-state
            kind="empty"
            [message]="'mobile.noNotifications' | hhTranslate"
          />
        } @else {
          <div class="notification-list">
            @for (item of items; track item.id) {
              <article
                class="notification"
                role="button"
                tabindex="0"
                [class.notification--unread]="!item.readAt"
                (click)="markRead(item)"
                (keydown.enter)="markRead(item)"
                (keydown.space)="markRead(item)"
              >
                <span class="notification__icon"
                  ><hh-mobile-icon name="notifications" size="small"
                /></span>
                <div class="notification__body">
                  <h2>{{ item.title }}</h2>
                  <p>{{ item.body }}</p>
                  <time [dateTime]="item.createdAt">{{
                    i18n.formatDateTime(item.createdAt)
                  }}</time>
                </div>
                @if (!item.readAt) {
                  <span class="notification__dot" aria-label="Unread"></span>
                }
              </article>
            }
          </div>
        }
      }
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .mobile-page {
        display: grid;
        gap: var(--space-md);
      }
      .unread {
        margin: 0;
        color: var(--text-secondary);
        font-size: var(--font-size-label);
      }
      .notification-list {
        display: grid;
        gap: var(--space-sm);
      }
      .notification {
        display: flex;
        align-items: flex-start;
        gap: var(--space-inset);
        padding: var(--space-list-item);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        cursor: pointer;
      }
      .notification--unread {
        border-color: color-mix(
          in srgb,
          var(--color-primary) 38%,
          var(--border-default)
        );
        background: color-mix(
          in srgb,
          var(--color-primary-soft) 34%,
          var(--surface-white)
        );
      }
      .notification__icon {
        display: grid;
        place-items: center;
        width: 34px;
        height: 34px;
        flex: 0 0 34px;
        border-radius: var(--radius-chip);
        background: var(--color-primary-soft);
        color: var(--color-primary);
      }
      .notification__body {
        min-width: 0;
        flex: 1;
      }
      h2 {
        margin: 0 0 var(--space-2xs);
        font-size: var(--font-size-body-emphasis);
      }
      p {
        margin: 0 0 var(--space-xs);
        color: var(--text-secondary);
        font-size: var(--font-size-label);
        line-height: 1.4;
      }
      time {
        color: var(--text-muted);
        font-size: var(--font-size-nav);
      }
      .notification__dot {
        width: var(--size-status-dot);
        height: var(--size-status-dot);
        margin-top: var(--space-snug);
        flex: 0 0 8px;
        border-radius: 50%;
        background: var(--color-primary);
      }
    `,
  ],
})
export class MobileNotificationsComponent implements OnInit {
  private readonly api = inject(MobileAdminApiService);
  private readonly changeDetector = inject(ChangeDetectorRef);
  readonly i18n = inject(HisHopeI18nService);
  items: MobileNotification[] = [];
  unread = 0;
  loading = false;
  error = "";
  ngOnInit(): void {
    this.load();
  }
  load(): void {
    this.loading = true;
    this.error = "";
    this.api
      .getNotifications()
      .pipe(
        finalize(() => {
          this.loading = false;
          this.changeDetector.detectChanges();
        }),
        catchError(() => {
          this.error = this.i18n.t(
            "mobile.notificationsLoadFailed",
            "Unable to load notifications.",
          );
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) {
          this.items = [...result.items];
          this.unread = result.unread;
        }
        this.changeDetector.detectChanges();
      });
  }
  markRead(item: MobileNotification): void {
    if (item.readAt) return;
    this.api.markNotificationRead(item.id).subscribe(() => {
      this.items = this.items.map((current) =>
        current.id === item.id
          ? { ...current, readAt: new Date().toISOString() }
          : current,
      );
      this.unread = Math.max(0, this.unread - 1);
      this.changeDetector.detectChanges();
    });
  }
  markAllRead(): void {
    if (!this.unread) return;
    this.api.markAllNotificationsRead().subscribe(() => {
      const now = new Date().toISOString();
      this.items = this.items.map((item) =>
        item.readAt ? item : { ...item, readAt: now },
      );
      this.unread = 0;
      this.changeDetector.detectChanges();
    });
  }
}
