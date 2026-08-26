import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { RouterLink } from "@angular/router";
import { CommerceApiService, Order } from "../../core/services/commerce-api.service";
import { HisHopeApiErrorMessageService as ApiErrorMessageService } from "@his-hope/frontend-foundation/i18n";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, HisHopeTranslatePipe],
  template: `
    <section class="page-shell page-shell--narrow">
      <div class="fx-container">
        <div class="page-head">
          <div>
            <p class="page-head__eyebrow">{{ 'buyer.orders.eyebrow' | hhTranslate }}</p>
            <h1>{{ 'buyer.orders.title' | hhTranslate }}</h1>
          </div>
        </div>

        @if (loading) {
          <div class="state">{{ 'buyer.orders.loading' | hhTranslate }}</div>
        } @else if (error) {
          <div class="state state--error">{{ error }}</div>
        } @else if (!orders.length) {
          <div class="state fx-card">{{ 'buyer.orders.empty' | hhTranslate }} <a routerLink="/catalog">{{ 'buyer.checkout' | hhTranslate }}</a></div>
        } @else {
          <div class="orders">
            @for (order of orders; track order.id) {
              <article class="order fx-card">
                <header>
                  <div>
                    <a [routerLink]="['/orders', order.id]"><strong>#{{ order.id.slice(0, 8) }}</strong></a>
                    <span>{{ i18n.formatDateTime(order.createdAt) }}</span>
                  </div>
                  <span class="status">{{ order.status }}</span>
                </header>
                <p class="amount">{{ i18n.formatCurrency(order.totalAmount, 'VND') }}</p>
                <ul>
                  @for (line of order.lines; track line.productId) {
                    <li>{{ line.name }} × {{ line.quantity }}</li>
                  }
                </ul>
              </article>
            }
          </div>
        }
      </div>
    </section>
  `,
  styles: [
    `
      .page-head__eyebrow { margin: 0 0 0.35rem; color: var(--color-primary); font-weight: var(--font-weight-extrabold); text-transform: uppercase; letter-spacing: 0.08em; font-size: var(--font-size-caption); }
      .page-head h1 { margin: 0 0 1.5rem; }
      .orders { display: grid; gap: 1rem; }
      .order { padding: 1.1rem; }
      header { display: flex; justify-content: space-between; gap: 1rem; align-items: center; }
      header span { display: block; color: var(--text-secondary); font-size: var(--font-size-label); }
      .status { text-transform: uppercase; color: var(--color-primary); font-weight: var(--font-weight-extrabold); font-size: var(--font-size-caption); }
      header a { color: inherit; }
      .amount { margin: 0.5rem 0; font-size: var(--font-size-title-sm); font-weight: var(--font-weight-bold); }
      ul { margin: 0; padding-left: 1rem; color: var(--text-secondary); }
      .state { padding: 2rem; text-align: center; }
      .state a { color: var(--color-primary); font-weight: var(--font-weight-bold); }
    `,
  ],
})
export class OrdersPageComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly errors = inject(ApiErrorMessageService);
  readonly i18n = inject(HisHopeI18nService);

  loading = true;
  orders: Order[] = [];
  error = "";

  ngOnInit(): void {
    this.api.getOrders().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => {
        this.orders = response.items ?? [];
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: (error) => {
        this.error = this.errors.message(error, "buyer.orders.error");
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }
}
