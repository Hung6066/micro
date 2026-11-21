import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink } from "@angular/router";
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
        <a routerLink="/orders" class="back-link">{{ 'buyer.orders.back' | hhTranslate }}</a>
        @if (loading) {
          <div class="state">{{ 'buyer.orders.loading' | hhTranslate }}</div>
        } @else if (error) {
          <div class="state state--error">{{ error }}</div>
        } @else if (order) {
          <div class="page-head">
            <div>
              <p class="page-head__eyebrow">{{ 'buyer.orders.detailEyebrow' | hhTranslate }}</p>
              <h1>#{{ order.id.slice(0, 8) }}</h1>
              <p>{{ i18n.formatDateTime(order.createdAt) }} · {{ order.status }}</p>
            </div>
          </div>
          <article class="order fx-card">
            <ul>
              @for (line of order.lines; track line.productId) {
                <li>
                  <span>{{ line.name }} × {{ line.quantity }}</span>
                  <strong>{{ i18n.formatCurrency(line.unitPrice * line.quantity, 'VND') }}</strong>
                </li>
              }
            </ul>
            <div class="total">
              <span>{{ 'buyer.total' | hhTranslate }}</span>
              <strong>{{ i18n.formatCurrency(order.totalAmount, 'VND') }}</strong>
            </div>
          </article>
        }
      </div>
    </section>
  `,
  styles: [`
    .back-link { display: inline-block; margin-bottom: 1.25rem; color: var(--color-primary); font-weight: var(--font-weight-bold); }
    .page-head__eyebrow { margin: 0 0 0.35rem; color: var(--color-primary); font-weight: var(--font-weight-extrabold); text-transform: uppercase; letter-spacing: 0.08em; font-size: var(--font-size-caption); }
    .page-head h1 { margin: 0 0 0.5rem; }
    .order { padding: 1.25rem; }
    ul { list-style: none; padding: 0; margin: 0 0 1rem; }
    li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.75rem 0; border-bottom: 1px solid var(--border-default); }
    .total { display: flex; justify-content: space-between; font-size: var(--font-size-title-sm); }
    .state { padding: 2rem; text-align: center; }
  `],
})
export class OrderDetailPageComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly errors = inject(ApiErrorMessageService);
  readonly i18n = inject(HisHopeI18nService);

  loading = true;
  error = "";
  order: Order | null = null;

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get("orderId");
    if (!orderId) {
      this.error = "buyer.orders.error";
      this.loading = false;
      return;
    }
    this.api.getOrder(orderId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (order) => {
        this.order = order;
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
