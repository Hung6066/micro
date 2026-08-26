import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { Router, RouterLink } from "@angular/router";
import { CommerceApiService } from "../../core/services/commerce-api.service";
import { HisHopeApiErrorMessageService as ApiErrorMessageService } from "@his-hope/frontend-foundation/i18n";
import { forkJoin } from "rxjs";
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
            <p class="page-head__eyebrow">{{ 'buyer.cart' | hhTranslate }}</p>
            <h1>{{ 'buyer.cart.title' | hhTranslate }}</h1>
          </div>
        </div>

        @if (loading) {
          <div class="state">{{ 'buyer.cart.loading' | hhTranslate }}</div>
        } @else if (error) {
          <div class="state state--error">{{ error }}</div>
        } @else if (!lines.length) {
          <div class="state fx-card">
            {{ 'buyer.cart.empty' | hhTranslate }} <a routerLink="/catalog">{{ 'buyer.catalog.back' | hhTranslate }}</a>
          </div>
        } @else {
          <div class="cart-panel fx-card">
            <ul class="lines">
              @for (line of lines; track line.productId) {
                <li>
                  <span>{{ line.name }} × {{ line.quantity }}</span>
                  <strong>{{ i18n.formatCurrency(line.unitPrice * line.quantity, 'VND') }}</strong>
                </li>
              }
            </ul>
            <div class="total">
              <span>{{ 'buyer.total' | hhTranslate }}</span>
              <strong>{{ i18n.formatCurrency(total, 'VND') }}</strong>
            </div>
            <button type="button" class="fx-btn-primary" [disabled]="checkingOut" (click)="checkout()">
              {{ checkingOut ? ('buyer.processing' | hhTranslate) : ('buyer.checkout' | hhTranslate) }}
            </button>
          </div>
        }
      </div>
    </section>
  `,
  styles: [
    `
      .page-head__eyebrow { margin: 0 0 0.35rem; color: var(--color-primary); font-weight: 800; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; }
      .page-head h1 { margin: 0 0 1.5rem; }
      .cart-panel { padding: 1.25rem; }
      .lines { list-style: none; padding: 0; margin: 0 0 1rem; }
      .lines li { display: flex; justify-content: space-between; gap: 1rem; padding: 0.75rem 0; border-bottom: 1px solid var(--border-default); }
      .total { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; font-size: 1.1rem; }
      .state { padding: 2rem; text-align: center; color: var(--text-secondary); }
      .state a { color: var(--color-primary); font-weight: 700; }
    `,
  ],
})
export class CartPageComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly errors = inject(ApiErrorMessageService);
  readonly i18n = inject(HisHopeI18nService);

  loading = true;
  checkingOut = false;
  lines: Array<{ productId: string; name: string; quantity: number; unitPrice: number }> = [];
  total = 0;
  error = "";

  ngOnInit(): void {
    forkJoin({ cart: this.api.getCart(), products: this.api.getProducts() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ cart, products }) => {
          const catalog = new Map((products.items ?? []).map((item) => [item.id, item]));
          this.lines = (cart.lines ?? [])
            .map((line) => {
              const product = catalog.get(line.productId);
              if (!product) return null;
              return {
                productId: line.productId,
                name: product.name,
                quantity: line.quantity,
                unitPrice: product.effectiveUnitPrice,
              };
            })
            .filter((line): line is NonNullable<typeof line> => line !== null);
          this.total = this.lines.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0);
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: (error) => {
          this.error = this.errors.message(error, "buyer.cart.error");
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  checkout(): void {
    this.checkingOut = true;
    this.api.checkout().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => void this.router.navigateByUrl("/orders"),
      error: (error) => {
        this.error = this.errors.message(error, "buyer.checkout.error");
        this.checkingOut = false;
        this.cdr.markForCheck();
      },
    });
  }
}
