import { FormsModule } from "@angular/forms";
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
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
  HisHopeSelectComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeCommerceOrderDto,
  HisHopeCommerceOrderListResponse,
} from "@his-hope/frontend-foundation/contracts";
import { environment } from "../../../environments/environment";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { HisHopeApiErrorMessageService as ApiErrorMessageService } from "@his-hope/frontend-foundation/i18n";
import { AdminDirectoryService, OperatorDirectoryUser } from "../../core/services/admin-directory.service";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    CurrencyPipe,
    DatePipe,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
    HisHopeSelectComponent,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'customerPortal.ordersTitle' | hhTranslate: 'Commerce orders'"
        [subtitle]="pageSubtitle"
      />
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="'customerPortal.loadingOrders' | hhTranslate: 'Loading orders…'"
        />
      } @else if (error) {
        <hh-state kind="error" [message]="error" />
      } @else if (!orders.length) {
        <hh-state kind="empty" [message]="'customerPortal.noOrders' | hhTranslate: 'No orders for the selected tenant.'" />
      } @else {
        <div class="orders">
          @for (order of orders; track order.id) {
            <article class="order">
              <header>
                <div>
                  <strong>{{ order.id.slice(0, 8) }}</strong>
                  <span>{{ order.createdAt | date: "medium" }}</span>
                </div>
                <hh-select [ngModel]="order.status" (ngModelChange)="updateStatus(order, $event)">
                  <option value="pending">{{ "customerPortal.orderStatusPending" | hhTranslate: "pending" }}</option>
                  <option value="confirmed">{{ "customerPortal.orderStatusConfirmed" | hhTranslate: "confirmed" }}</option>
                  <option value="shipped">{{ "customerPortal.orderStatusShipped" | hhTranslate: "shipped" }}</option>
                  <option value="cancelled">{{ "customerPortal.orderStatusCancelled" | hhTranslate: "cancelled" }}</option>
                </hh-select>
              </header>
              <p>
                {{
                  "customerPortal.orderBuyerLine"
                    | hhTranslate
                      : "Buyer: {{buyer}} · {{amount}}"
                      : {
                          buyer: buyerLabel(order.buyerUserId),
                          amount: (order.totalAmount | currency) ?? "",
                        }
                }}
              </p>
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
      :host {
        font-family: var(--font-sans);
      }
      .orders {
        display: grid;
        gap: var(--space-md);
      }
      .order {
        border: 1px solid var(--border-subtle);
        border-radius: var(--radius-card);
        padding: var(--space-md);
      }
      header {
        display: flex;
        justify-content: space-between;
        gap: var(--space-md);
        align-items: center;
      }
      header span {
        display: block;
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      ul {
        font-size: var(--font-size-body);
      }
    `,
  ],
})
export class OrdersPageComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly directory = inject(AdminDirectoryService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly errors = inject(ApiErrorMessageService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  loading = true;
  error = "";
  orders: HisHopeCommerceOrderDto[] = [];
  users: OperatorDirectoryUser[] = [];
  tenantLabel: string | null = null;

  get pageSubtitle(): string {
    this.i18n.locale();
    return this.i18n.t("customerPortal.tenantScope", "Tenant: {{tenant}}", {
      tenant:
        this.tenantLabel ??
        this.i18n.t("customerPortal.tenantUnknown", "—"),
    });
  }

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
    this.directory.getUsers().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => { this.users = result.items ?? []; this.cdr.markForCheck(); },
      error: () => { this.users = []; this.cdr.markForCheck(); },
    });
    this.http
      .get<HisHopeCommerceOrderListResponse>(`${environment.commerceApiUrl}/orders`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.orders = response.items ?? [];
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: (error) => {
          this.error = this.errors.message(error, "customerPortal.ordersLoadFailed");
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  buyerLabel(userId: string): string {
    const user = this.users.find((candidate) => candidate.id === userId);
    if (!user) return "Buyer account";
    const name = [user.firstName, user.lastName].filter(Boolean).join(" ").trim();
    return name || user.username || user.email;
  }

  updateStatus(order: HisHopeCommerceOrderDto, status: string): void {
    this.http
      .patch<HisHopeCommerceOrderDto>(
        `${environment.commerceApiUrl}/orders/${order.id}/status`,
        { status },
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.orders = this.orders.map((candidate) =>
            candidate.id === updated.id ? updated : candidate,
          );
          this.cdr.markForCheck();
        },
        error: (error) => {
          this.error = this.errors.message(error, "customerPortal.orderStatusUpdateFailed");
          this.cdr.markForCheck();
        },
      });
  }
}
