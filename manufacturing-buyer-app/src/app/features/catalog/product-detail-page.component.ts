import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from "@angular/core";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { catchError, of, switchMap } from "rxjs";
import {
  HisHopeApiErrorMessageService,
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { CommerceApiService, Product } from "../../core/services/commerce-api.service";
import {
  productCategoryKey,
  productImageUrl,
} from "../../core/utils/product-media.util";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, HisHopeTranslatePipe],
  template: `
    <section class="page-shell page-shell--narrow" data-testid="buyer-product-detail">
      <div class="fx-container">
        <a routerLink="/catalog" class="back-link">{{ "buyer.catalog.back" | hhTranslate }}</a>
        @if (loading()) {
          <div class="state">{{ "buyer.catalog.loading" | hhTranslate }}</div>
        } @else if (error()) {
          <div class="state state--error">{{ error() | hhTranslate }}</div>
        } @else if (product(); as item) {
          <article class="product-detail fx-card">
            <div class="product-detail__media">
              <img [src]="imageUrl(item)" [alt]="item.name" />
            </div>
            <div class="product-detail__body">
              <p class="page-head__eyebrow">{{ categoryKey(item) | hhTranslate }}</p>
              <h1>{{ item.name }}</h1>
              <p class="product-detail__sku">{{ "buyer.catalog.sku" | hhTranslate }}: {{ item.sku }}</p>
              <p class="product-detail__description">{{ item.description }}</p>
              <div class="product-detail__price">
                <span>{{ "buyer.catalog.detail" | hhTranslate }}</span>
                <strong>{{ i18n.formatCurrency(item.unitPrice, "VND") }}</strong>
              </div>
              <dl class="product-detail__facts">
                <div><dt>{{ "buyer.catalog.listPrice" | hhTranslate }}</dt><dd>{{ i18n.formatCurrency(item.listUnitPrice, "VND") }}</dd></div>
                <div><dt>{{ "buyer.catalog.minOrder" | hhTranslate }}</dt><dd>{{ item.minOrderQty }}</dd></div>
                <div><dt>{{ "buyer.catalog.privateLabel" | hhTranslate }}</dt><dd>{{ (item.supportsPrivateLabel ? "buyer.catalog.yes" : "buyer.catalog.no") | hhTranslate }}</dd></div>
                <div><dt>{{ "buyer.catalog.export" | hhTranslate }}</dt><dd>{{ (item.supportsExport ? "buyer.catalog.yes" : "buyer.catalog.no") | hhTranslate }}</dd></div>
              </dl>
              <button type="button" class="fx-btn-primary" [disabled]="adding()" (click)="addToCart(item)">
                {{ (adding() ? "buyer.processing" : "buyer.catalog.addToCart") | hhTranslate }}
              </button>
            </div>
          </article>
        }
      </div>
    </section>
  `,
  styles: [`
    .back-link { display: inline-block; margin-bottom: var(--space-xl); color: var(--color-primary); font-weight: var(--font-weight-bold); }
    .product-detail { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); overflow: hidden; }
    .product-detail__media { min-height: 420px; background: var(--surface-muted); }
    .product-detail__media img { width: 100%; height: 100%; min-height: 420px; object-fit: cover; display: block; }
    .product-detail__body { padding: var(--space-3xl); }
    .page-head__eyebrow { margin: 0 0 var(--space-xs); color: var(--color-primary); font-size: var(--font-size-caption); font-weight: var(--font-weight-extrabold); text-transform: uppercase; letter-spacing: .08em; }
    h1 { margin: 0; color: var(--text-primary); font-size: var(--font-size-display); line-height: var(--leading-tight); }
    .product-detail__sku, .product-detail__description { color: var(--text-secondary); }
    .product-detail__description { margin: var(--space-xl) 0; line-height: var(--leading-body); }
    .product-detail__price { display: flex; justify-content: space-between; align-items: baseline; gap: var(--space-md); margin-bottom: var(--space-xl); color: var(--text-secondary); }
    .product-detail__price strong { color: var(--color-primary); font-size: var(--font-size-title); }
    .product-detail__facts { display: grid; gap: var(--space-sm); margin: 0 0 var(--space-xl); }
    .product-detail__facts div { display: flex; justify-content: space-between; gap: var(--space-md); padding-bottom: var(--space-sm); border-bottom: 1px solid var(--border-light); }
    dt { color: var(--text-secondary); } dd { margin: 0; color: var(--text-primary); font-weight: var(--font-weight-semibold); text-align: right; }
    .state { padding: var(--space-3xl); text-align: center; color: var(--text-secondary); }
    @media (max-width: 760px) { .product-detail { grid-template-columns: 1fr; } .product-detail__media, .product-detail__media img { min-height: 280px; } .product-detail__body { padding: var(--space-xl); } }
  `],
})
export class ProductDetailPageComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly errors = inject(HisHopeApiErrorMessageService);
  readonly i18n = inject(HisHopeI18nService);
  readonly product = signal<Product | null>(null);
  readonly loading = signal(true);
  readonly error = signal("");
  readonly adding = signal(false);

  ngOnInit(): void {
    this.route.paramMap.pipe(
      switchMap((params) => this.api.getProduct(params.get("productId") ?? "")),
      catchError((error) => {
        this.error.set(this.errors.message(error, "buyer.catalog.error"));
        return of(null);
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe((product) => {
      this.product.set(product);
      this.loading.set(false);
      this.cdr.markForCheck();
    });
  }

  categoryKey(product: Product): string { return productCategoryKey(product.sku); }
  imageUrl(product: Product): string { return productImageUrl(product.sku); }

  addToCart(product: Product): void {
    this.adding.set(true);
    this.api.getCart().pipe(
      switchMap((cart) => {
        const lines = [...(cart.lines ?? [])];
        const existing = lines.find((line) => line.productId === product.id);
        if (existing) existing.quantity += Math.max(product.minOrderQty, 1);
        else lines.push({ productId: product.id, quantity: Math.max(product.minOrderQty, 1) });
        return this.api.updateCart(lines);
      }),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: () => void this.router.navigateByUrl("/cart"),
      error: () => { this.adding.set(false); this.cdr.markForCheck(); },
    });
  }
}
