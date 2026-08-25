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
        } @else if (!orders.length) {
          <div class="state fx-card">{{ 'buyer.orders.empty' | hhTranslate }} <a routerLink="/catalog">{{ 'buyer.checkout' | hhTranslate }}</a></div>
        } @else {
          <div class="orders">
            @for (order of orders; track order.id) {
              <article class="order fx-card">
                <header>
                  <div>
                    <strong>#{{ order.id.slice(0, 8) }}</strong>
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
      .page-head__eyebrow { margin: 0 0 0.35rem; color: var(--color-primary); font-weight: 800; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; }
      .page-head h1 { margin: 0 0 1.5rem; }
      .orders { display: grid; gap: 1rem; }
      .order { padding: 1.1rem; }
      header { display: flex; justify-content: space-between; gap: 1rem; align-items: center; }
      header span { display: block; color: var(--text-secondary); font-size: 0.85rem; }
      .status { text-transform: uppercase; color: var(--color-primary); font-weight: 800; font-size: 0.78rem; }
      .amount { margin: 0.5rem 0; font-size: 1.1rem; font-weight: 700; }
      ul { margin: 0; padding-left: 1rem; color: var(--text-secondary); }
      .state { padding: 2rem; text-align: center; }
      .state a { color: var(--color-primary); font-weight: 700; }
    `,
  ],
})
export class OrdersPageComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly i18n = inject(HisHopeI18nService);

  loading = true;
  orders: Order[] = [];

  ngOnInit(): void {
    this.api.getOrders().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => {
        this.orders = response.items ?? [];
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }
}
