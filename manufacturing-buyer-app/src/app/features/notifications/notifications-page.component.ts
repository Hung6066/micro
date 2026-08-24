import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { CommerceApiService, NotificationItem } from "../../core/services/commerce-api.service";
import { HisHopeApiErrorMessageService as ApiErrorMessageService } from "@his-hope/frontend-foundation/i18n";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [HisHopeTranslatePipe],
  template: `
    <section class="page-shell page-shell--narrow">
      <div class="fx-container">
        <div class="page-head">
          <div>
            <p class="page-head__eyebrow">{{ 'buyer.notifications' | hhTranslate }}</p>
            <h1>{{ 'buyer.notifications.title' | hhTranslate }}</h1>
          </div>
        </div>

        @if (loading) {
          <div class="state">{{ 'buyer.notifications.loading' | hhTranslate }}</div>
        } @else if (error) {
          <div class="state state--error">{{ error }}</div>
        } @else if (!items.length) {
          <div class="state fx-card">{{ 'buyer.notifications.empty' | hhTranslate }}</div>
        } @else {
          <div class="list">
            @for (item of items; track item.id) {
              <article class="item fx-card">
                <strong>{{ item.title }}</strong>
                <p>{{ item.message }}</p>
                <small>{{ i18n.formatDateTime(item.createdAt) }}</small>
              </article>
            }
          </div>
        }
      </div>
    </section>
  `,
  styles: [
    `
      .page-head__eyebrow { margin: 0 0 0.35rem; color: var(--color-primary); font-weight: 800; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; }
      .page-head h1 { margin: 0 0 1.5rem; }
      .list { display: grid; gap: 0.85rem; }
      .item { padding: 1rem; }
      .item p { margin: 0.35rem 0; color: var(--text-secondary); line-height: 1.6; }
      .item small { color: var(--text-secondary); }
      .state { padding: 2rem; text-align: center; color: var(--text-secondary); }
    `,
  ],
})
export class NotificationsPageComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly errors = inject(ApiErrorMessageService);
  readonly i18n = inject(HisHopeI18nService);

  loading = true;
  items: NotificationItem[] = [];
  error = "";

  ngOnInit(): void {
    this.api.getNotifications().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => {
        this.items = response.items ?? [];
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (error) => {
        this.error = this.errors.message(error, "buyer.notifications.error");
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }
}
