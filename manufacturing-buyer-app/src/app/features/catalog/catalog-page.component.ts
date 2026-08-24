import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import { MatButtonModule } from "@angular/material/button";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { CommerceApiService, Product } from "../../core/services/commerce-api.service";
import {
  ProductSort,
  productCategoryLabel,
  productImageUrl,
  sortProducts,
} from "../../core/utils/product-media.util";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, FormsModule, HisHopeTranslatePipe],
  templateUrl: "./catalog-page.component.html",
  styleUrls: ["./catalog-page.component.scss"],
})
export class CatalogPageComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly i18n = inject(HisHopeI18nService);

  products: Product[] = [];
  filtered: Product[] = [];
  loading = true;
  error = "";
  searchQuery = "";
  sortBy: ProductSort = "default";

  readonly sortOptions: { value: ProductSort; label: string }[] = [
    { value: "default", label: "buyer.catalog.sort.default" },
    { value: "price-asc", label: "buyer.catalog.sort.priceAsc" },
    { value: "price-desc", label: "buyer.catalog.sort.priceDesc" },
    { value: "name", label: "buyer.catalog.sort.name" },
  ];

  ngOnInit(): void {
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      this.searchQuery = params.get("q") ?? "";
      this.applyFilter();
      this.cdr.markForCheck();
    });

    this.api.getProducts().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => {
        this.products = response.items ?? [];
        this.applyFilter();
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.error = "buyer.catalog.error";
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  onSortChange(): void {
    this.applyFilter();
    this.cdr.markForCheck();
  }

  categoryLabel(product: Product): string {
    return productCategoryLabel(product.sku);
  }

  imageUrl(product: Product): string {
    return productImageUrl(product.sku);
  }

  addToCart(product: Product): void {
    this.api.getCart().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (cart) => {
        const lines = [...(cart.lines ?? [])];
        const existing = lines.find((line) => line.productId === product.id);
        if (existing) {
          existing.quantity += 1;
        } else {
          lines.push({ productId: product.id, quantity: 1 });
        }
        this.api.updateCart(lines).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
          next: () => void this.router.navigateByUrl("/cart"),
        });
      },
    });
  }

  private applyFilter(): void {
    const query = this.searchQuery.trim().toLowerCase();
    const matched = !query
      ? this.products
      : this.products.filter(
          (product) =>
            product.name.toLowerCase().includes(query) ||
            product.sku.toLowerCase().includes(query) ||
            product.description.toLowerCase().includes(query),
        );
    this.filtered = sortProducts(matched, this.sortBy);
  }
}
