import { DecimalPipe } from "@angular/common";
import { AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { HisHopeManufacturingSalesAllocationDto, HisHopeManufacturingAvailabilityDto, HisHopeCommerceOrderDto } from "@his-hope/frontend-foundation/contracts";
import { HisHopeApiErrorMessageService as ApiErrorMessageService, HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeActionButtonComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTabsComponent , HisHopeSelectComponent} from "@his-hope/frontend-foundation/ui";
import { ManufacturingApiService } from "../../core/services/manufacturing-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { CommerceApiService } from "../../core/services/commerce-api.service";
import { AdminDirectoryService, OperatorDirectoryUser } from "../../core/services/admin-directory.service";
import { portalEnumLabel } from "../../core/utils/portal-label.util";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, HisHopeActionButtonComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTabsComponent, HisHopeTranslatePipe, HisHopeSelectComponent],
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'customerPortal.salesAllocationTitle' | hhTranslate: 'Sales allocation & ATP'" [subtitle]="pageSubtitle" />
      <hh-tabs label="Sales allocation sections"><button role="tab" type="button" [attr.aria-selected]="activeTab === 'allocate'" [class.active]="activeTab === 'allocate'" (click)="selectTab('allocate')">{{ 'customerPortal.allocateSales' | hhTranslate: 'Allocate inventory' }}</button><button role="tab" type="button" [attr.aria-selected]="activeTab === 'availability'" [class.active]="activeTab === 'availability'" (click)="selectTab('availability')">{{ 'customerPortal.availableToPromise' | hhTranslate: 'Available to promise' }}</button><button role="tab" type="button" [attr.aria-selected]="activeTab === 'result'" [class.active]="activeTab === 'result'" (click)="selectTab('result')">{{ 'customerPortal.allocationResult' | hhTranslate: 'Allocation result' }}</button></hh-tabs>
      @if (loading) { <hh-state kind="loading" [message]="'customerPortal.loadingSalesAllocation' | hhTranslate: 'Loading allocation workspace…'" /> }
      @else {
        <section class="section form-section">
          <div class="section-heading"><div><p class="eyebrow">{{ 'customerPortal.salesGovernance' | hhTranslate: 'Sales fulfillment' }}</p><h2>{{ 'customerPortal.allocateSales' | hhTranslate: 'Allocate inventory' }}</h2></div></div>
          <form class="allocation-form" (ngSubmit)="allocate()">
            <label>{{ 'customerPortal.productSku' | hhTranslate: 'Product SKU' }}<input name="sku" [(ngModel)]="draft.sku" (blur)="loadAvailability()" required /></label>
            <label for="orderId">{{ 'customerPortal.salesOrder' | hhTranslate: 'Sales order' }}
              <hh-select id="orderId" name="orderId" [(ngModel)]="draft.salesOrderId" required>
                <option value="">{{ 'customerPortal.selectSalesOrder' | hhTranslate: 'Select sales order' }}</option>
                @for (order of orders; track order.id) { <option [value]="order.id">{{ order.lines[0]?.name || order.lines[0]?.sku || ('customerPortal.unnamedOrder' | hhTranslate: 'Sales order') }} · {{ buyerLabel(order.buyerUserId) }} · {{ salesOrderStatusLabel(order.status) }} · {{ order.totalAmount | number:'1.0-0' }}</option> }
              </hh-select>
            </label>
            <label>{{ 'customerPortal.allocationQuantity' | hhTranslate: 'Quantity' }}<input name="quantity" type="number" min="0.001" step="0.001" [(ngModel)]="draft.quantity" required /></label>
            <div class="wide actions"><hh-action-button type="button" kind="secondary" icon="inventory" [label]="'customerPortal.checkAvailability' | hhTranslate: 'Check ATP'" [disabled]="busy || !draft.sku.trim()" (pressed)="loadAvailability()" /><hh-action-button type="submit" kind="primary" icon="shopping_cart_checkout" [label]="'customerPortal.allocateSales' | hhTranslate: 'Allocate inventory'" [disabled]="busy" /></div>
          </form>
          @if (actionError) { <p class="action-error" role="alert">{{ actionError }}</p> }
        </section>
        @if (availability) { <section class="section atp-card"><p class="eyebrow">{{ 'customerPortal.availableToPromise' | hhTranslate: 'Available to promise' }}</p><strong>{{ availability.availableToPromiseQuantity | number:'1.0-3' }} {{ availability.uom }}</strong><p class="meta">{{ 'customerPortal.atpBreakdown' | hhTranslate: 'Released {{released}} · Reserved {{reserved}}' : { released: ((availability.releasedQuantity | number:'1.0-3') ?? ''), reserved: ((availability.reservedQuantity | number:'1.0-3') ?? '') } }}</p></section> }
        @if (allocation) { <section class="section result-card"><div class="section-heading"><div><p class="eyebrow">{{ 'customerPortal.allocationResult' | hhTranslate: 'Allocation result' }}</p><h2>{{ allocation.sku }}</h2></div><span class="status">{{ allocation.allocatedQuantity | number:'1.0-3' }} / {{ allocation.requestedQuantity | number:'1.0-3' }}</span></div><p>{{ 'customerPortal.reservationCount' | hhTranslate: '{{count}} reservations created' : { count: allocation.reservations.length } }}</p>@if (allocation.shortageQuantity > 0) { <p class="shortage">{{ 'customerPortal.shortageDetected' | hhTranslate: 'Shortage: {{quantity}}' : { quantity: ((allocation.shortageQuantity | number:'1.0-3') ?? '') } }}</p> }</section> }
      }
      @if (!loading && error) { <hh-state kind="error" [message]="error" (retry)="load()" /> }
    </hh-page-layout>
  `,
  styles: [`:host{font-family:var(--font-sans)}.section{margin-bottom:var(--space-lg)}.section-heading{display:flex;align-items:center;justify-content:space-between;gap:var(--space-md)}.eyebrow,.meta{color:var(--text-secondary);font-size:var(--font-size-caption);margin:0}.allocation-form{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-md);padding:var(--space-md);background:var(--surface-muted);border-radius:var(--radius-card)}label{display:grid;gap:var(--space-2xs);color:var(--text-primary);font-size:var(--font-size-caption)}input,select{border:1px solid var(--border-subtle);border-radius:var(--radius-control);padding:var(--space-sm);background:var(--surface);color:var(--text-primary);font:inherit}.wide{grid-column:1/-1}.actions{display:flex;justify-content:flex-end;gap:var(--space-sm);flex-wrap:wrap}.atp-card,.result-card{padding:var(--space-md);border:1px solid var(--border-subtle);border-radius:var(--radius-card);background:var(--surface);color:var(--text-primary)}.atp-card strong{display:block;font-size:var(--font-size-title)}.status{padding:var(--space-2xs) var(--space-sm);border-radius:var(--radius-badge);background:var(--color-success-subtle);color:var(--color-success)}.shortage,.action-error{color:var(--color-danger)}@media(max-width:700px){.allocation-form{grid-template-columns:1fr}.wide{grid-column:auto}.section-heading{align-items:flex-start;flex-direction:column}}`],
})
export class SalesAllocationPageComponent implements OnInit, AfterViewInit {
  activeTab = "allocate";
  selectTab(tab: string): void { this.activeTab = tab; if (tab === "availability" && !this.availability && this.draft.sku.trim()) this.loadAvailability(); if (tab === "result") this.loadAllocations(); this.applyTabVisibility(); this.cdr.markForCheck(); }
  ngAfterViewInit(): void { const observer = new MutationObserver(() => { if (document.querySelectorAll("section.section").length) { this.applyTabVisibility(); observer.disconnect(); } }); observer.observe(document.body, { childList: true, subtree: true }); this.applyTabVisibility(); }
  private applyTabVisibility(): void { const all = Array.from(document.querySelectorAll<HTMLElement>("section.section")); all.forEach(section => section.hidden = true); if (this.activeTab === "allocate") document.querySelector<HTMLElement>("section.form-section")?.removeAttribute("hidden"); if (this.activeTab === "availability") document.querySelector<HTMLElement>("section.atp-card")?.removeAttribute("hidden"); if (this.activeTab === "result") document.querySelector<HTMLElement>("section.result-card")?.removeAttribute("hidden"); }
  private readonly api = inject(ManufacturingApiService); private readonly commerceApi = inject(CommerceApiService); private readonly directory = inject(AdminDirectoryService); private readonly tenantContext = inject(TenantContextService); private readonly i18n = inject(HisHopeI18nService); private readonly errors = inject(ApiErrorMessageService); private readonly cdr = inject(ChangeDetectorRef); private readonly destroyRef = inject(DestroyRef);
  availability: HisHopeManufacturingAvailabilityDto | null = null; allocation: HisHopeManufacturingSalesAllocationDto | null = null; allocations: HisHopeManufacturingSalesAllocationDto[] = []; orders: HisHopeCommerceOrderDto[] = []; users: OperatorDirectoryUser[] = []; loading = true; busy = false; error = ""; actionError = ""; tenantLabel: string | null = null;
  draft = { sku: "", salesOrderId: "", quantity: 1 };
  get pageSubtitle(): string { this.i18n.locale(); return this.i18n.t("customerPortal.tenantScope", "Tenant: {{tenant}}", { tenant: this.tenantLabel ?? "—" }); }
  ngOnInit(): void { this.tenantContext.activeTenantLabel$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((label) => { this.tenantLabel = label; this.cdr.markForCheck(); }); this.tenantContext.activeTenantKey$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load()); }
  load(): void { this.loading = true; this.directory.getUsers().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (result) => { this.users = result.items ?? []; this.loadOrders(); }, error: () => { this.users = []; this.loadOrders(); } }); }
  private loadOrders(): void { this.commerceApi.getOrders().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (response) => { this.orders = response.items ?? []; this.loading = false; this.cdr.markForCheck(); }, error: (error) => { this.error = this.errors.message(error, "customerPortal.salesOrdersLoadFailed"); this.loading = false; this.cdr.markForCheck(); } }); }
  buyerLabel(userId: string): string { const user = this.users.find((candidate) => candidate.id === userId); if (!user) return "Buyer account"; const name = [user.firstName, user.lastName].filter(Boolean).join(" ").trim(); return name || user.username || user.email; }
  salesOrderStatusLabel(status: string): string { return portalEnumLabel(this.i18n, "salesOrderStatus", status); }
  loadAvailability(): void { const sku = this.draft.sku.trim(); if (!sku) return; this.busy = true; this.actionError = ""; this.api.getAvailability(sku).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (result) => { this.availability = result; this.busy = false; this.cdr.markForCheck(); }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.availabilityLoadFailed"); this.busy = false; this.cdr.markForCheck(); } }); }
  loadAllocations(): void { this.api.getSalesAllocations({ limit: 100 }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.allocations = items ?? []; this.allocation = this.allocations[0] ?? null; this.cdr.markForCheck(); }, error: () => { this.allocations = []; this.allocation = null; this.cdr.markForCheck(); } }); }
  allocate(): void { const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i; if (!this.draft.sku.trim() || !uuid.test(this.draft.salesOrderId.trim()) || this.draft.quantity <= 0) { this.actionError = this.i18n.t("customerPortal.allocationFormInvalid", "SKU, valid sales order ID and positive quantity are required."); return; } this.busy = true; this.actionError = ""; this.api.allocateSales(this.draft.sku.trim(), { salesOrderId: this.draft.salesOrderId.trim(), quantity: this.draft.quantity }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (result) => { this.allocation = result; this.allocations = [result, ...this.allocations.filter(x => x.salesOrderId !== result.salesOrderId || x.sku !== result.sku)]; this.busy = false; this.loadAvailability(); this.cdr.markForCheck(); }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.allocationFailed"); this.busy = false; this.cdr.markForCheck(); } }); }
}
