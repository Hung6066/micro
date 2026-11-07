import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { DatePipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeApiErrorMessageService as ApiErrorMessageService } from "@his-hope/frontend-foundation/i18n";
import {
  CommerceApiService,
  ProductCatalogItem,
  Rfq,
} from "../../core/services/commerce-api.service";

interface RfqDraftLine {
  productId: string;
  quantity: number;
  notes: string;
}

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, MatButtonModule, DatePipe, HisHopeTranslatePipe],
  templateUrl: "./rfq-page.component.html",
  styleUrls: ["./rfq-page.component.scss"],
})
export class RfqPageComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly errors = inject(ApiErrorMessageService);
  readonly i18n = inject(HisHopeI18nService);

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal("");
  readonly statusKey = signal("");
  readonly products = signal<ProductCatalogItem[]>([]);
  readonly rfqs = signal<Rfq[]>([]);
  message = "";
  lines: RfqDraftLine[] = [];

  ngOnInit(): void {
    this.api
      .getProducts()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          const items = response.items ?? [];
          this.products.set(items);
          this.lines = items.slice(0, 1).map((product) => ({
            productId: product.id,
            quantity: Math.max(product.minOrderQty, 1),
            notes: "",
          }));
          this.loading.set(false);
        },
        error: (err) => {
          this.error.set(this.errors.message(err, "buyer.rfq.errorLoad"));
          this.loading.set(false);
        },
      });

    this.loadRfqs();
  }

  productName(productId: string): string {
    return this.products().find((product) => product.id === productId)?.name ?? productId;
  }

  addLine(): void {
    const first = this.products()[0];
    if (!first) return;
    this.lines = [
      ...this.lines,
      { productId: first.id, quantity: Math.max(first.minOrderQty, 1), notes: "" },
    ];
  }

  removeLine(index: number): void {
    if (this.lines.length <= 1) return;
    this.lines = this.lines.filter((_, i) => i !== index);
  }

  onProductChange(line: RfqDraftLine): void {
    const product = this.products().find((item) => item.id === line.productId);
    if (product && line.quantity < product.minOrderQty) {
      line.quantity = product.minOrderQty;
    }
  }

  submit(): void {
    this.submitting.set(true);
    this.statusKey.set("");
    this.api
      .createRfq({
        message: this.message,
        lines: this.lines.map((line) => ({
          productId: line.productId,
          quantity: line.quantity,
          notes: line.notes || null,
        })),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.statusKey.set("buyer.rfq.success");
          this.message = "";
          this.submitting.set(false);
          this.loadRfqs();
        },
        error: (err) => {
          this.statusKey.set(this.errors.message(err, "buyer.rfq.errorSubmit"));
          this.submitting.set(false);
        },
      });
  }

  private loadRfqs(): void {
    this.api
      .getRfqs()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => this.rfqs.set(response.items ?? []),
      });
  }
}
