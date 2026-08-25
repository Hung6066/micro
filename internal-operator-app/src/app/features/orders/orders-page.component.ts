import { CurrencyPipe, DatePipe } from "@angular/common";
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { HttpClient } from "@angular/common/http";
import { MatButtonModule } from "@angular/material/button";
import { MatSelectModule } from "@angular/material/select";
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
} from "@his-hope/frontend-foundation/ui";
import { environment } from "../../../environments/environment";
import { TenantContextService } from "../../core/services/tenant-context.service";

interface OrderLine {
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
}

interface Order {
  id: string;
  tenantKey: string;
  buyerUserId: string;
  status: string;
  totalAmount: number;
  createdAt: string;
  lines: OrderLine[];
}

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    MatSelectModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        title="Commerce orders"
        [subtitle]="'Tenant: ' + (tenantLabel ?? '—')"
      />
      @if (loading) {
        <hh-state kind="loading" message="Loading orders..." />
      } @else if (error) {
        <hh-state kind="error" [message]="error" />
      } @else if (!orders.length) {
        <hh-state kind="empty" message="No orders for the selected tenant." />
      } @else {
        <div class="orders">
          @for (order of orders; track order.id) {
            <article class="order">
              <header>
                <div>
                  <strong>{{ order.id.slice(0, 8) }}</strong>
                  <span>{{ order.createdAt | date: "medium" }}</span>
                </div>
                <mat-select [value]="order.status" (selectionChange)="updateStatus(order, $event.value)">
                  <mat-option value="pending">pending</mat-option>
                  <mat-option value="confirmed">confirmed</mat-option>
                  <mat-option value="shipped">shipped</mat-option>
                  <mat-option value="cancelled">cancelled</mat-option>
                </mat-select>
              </header>
              <p>Buyer: {{ order.buyerUserId }} · {{ order.totalAmount | currency }}</p>
              <ul>
                @for (line of order.lines; track line.productId) {
                  <li>{{ line.name }} × {{ line.quantity }}</li>
                }
              </ul>
            </article>
          }
        </div>
      }
    </hh-page-layout>
  `,
  styles: [
    `
      .orders { display: grid; gap: var(--space-md); }
      .order { border: 1px solid var(--border-subtle); border-radius: var(--radius-md); padding: var(--space-md); }
      header { display: flex; justify-content: space-between; gap: var(--space-md); align-items: center; }
      header span { display: block; color: var(--text-secondary); font-size: 12px; }
    `,
  ],
})
export class OrdersPageComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly tenantContext = inject(TenantContextService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  loading = true;
  error = "";
  orders: Order[] = [];
  tenantLabel: string | null = null;

  ngOnInit(): void {
    this.tenantContext.activeTenantLabel$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((label) => {
        this.tenantLabel = label;
        this.cdr.markForCheck();
      });

    this.tenantContext.activeTenantKey$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadOrders());
  }

  loadOrders(): void {
    this.loading = true;
    this.error = "";
    this.http
      .get<{ items: Order[] }>(`${environment.commerceApiUrl}/orders`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.orders = response.items ?? [];
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.error = "Unable to load commerce orders. Switch to customer-factory-x tenant.";
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  updateStatus(order: Order, status: string): void {
    this.http
      .patch<Order>(`${environment.commerceApiUrl}/orders/${order.id}/status`, { status })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.orders = this.orders.map((candidate) =>
            candidate.id === updated.id ? updated : candidate,
          );
          this.cdr.markForCheck();
        },
      });
  }
}
