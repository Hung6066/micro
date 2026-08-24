import { DatePipe, DecimalPipe } from "@angular/common";
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { HisHopeSalesForecastDto, HisHopeSalesForecastMaterialRequirementDto } from "@his-hope/frontend-foundation/contracts";
import { HisHopeApiErrorMessageService as ApiErrorMessageService, HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeActionButtonComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent } from "@his-hope/frontend-foundation/ui";
import { ManufacturingApiService } from "../../core/services/manufacturing-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, FormsModule, HisHopeActionButtonComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTranslatePipe],
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'customerPortal.forecastTitle' | hhTranslate: 'Sales forecast & planning'" [subtitle]="pageSubtitle" />
      @if (loading) { <hh-state kind="loading" [message]="'customerPortal.loadingForecasts' | hhTranslate: 'Loading forecasts…'" /> }
      @else {
        <section class="section form-section">
          <div class="section-heading"><div><p class="eyebrow">{{ 'customerPortal.forecastGovernance' | hhTranslate: 'Demand planning' }}</p><h2>{{ 'customerPortal.createForecast' | hhTranslate: 'Create forecast' }}</h2></div></div>
          <form class="forecast-form" (ngSubmit)="create()">
            <label>{{ 'customerPortal.productSku' | hhTranslate: 'Product SKU' }}<input name="sku" [(ngModel)]="draft.productSku" required /></label>
            <label>{{ 'customerPortal.forecastQuantity' | hhTranslate: 'Quantity' }}<input name="quantity" type="number" min="0.001" step="0.001" [(ngModel)]="draft.quantity" required /></label>
            <label>{{ 'customerPortal.forecastUom' | hhTranslate: 'UOM' }}<input name="uom" [(ngModel)]="draft.uom" required /></label>
            <label>{{ 'customerPortal.forecastStart' | hhTranslate: 'Period start' }}<input name="periodStart" type="date" [(ngModel)]="draft.periodStart" required /></label>
            <label>{{ 'customerPortal.forecastEnd' | hhTranslate: 'Period end' }}<input name="periodEnd" type="date" [(ngModel)]="draft.periodEnd" required /></label>
            <label>{{ 'customerPortal.forecastSource' | hhTranslate: 'Source' }}<input name="source" [(ngModel)]="draft.source" required /></label>
            <div class="wide actions"><hh-action-button type="submit" kind="primary" icon="add_chart" [label]="'customerPortal.createForecast' | hhTranslate: 'Create forecast'" [disabled]="saving" /></div>
          </form>
          @if (actionError) { <p class="action-error" role="alert">{{ actionError }}</p> }
        </section>
        <section class="section forecast-grid">
          @for (forecast of forecasts; track forecast.id) {
            <article class="card"><header><div><strong>{{ forecast.productSku }}</strong><p class="meta">{{ forecast.periodStart | date:'mediumDate' }} – {{ forecast.periodEnd | date:'mediumDate' }}</p></div><span class="version">v{{ forecast.version }}</span></header><p class="quantity">{{ forecast.quantity | number:'1.0-3' }} {{ forecast.uom }}</p><p class="meta">{{ forecast.source }} · {{ forecast.actor }}</p><footer><hh-action-button kind="secondary" icon="calculate" [label]="'customerPortal.calculateRequirements' | hhTranslate: 'Calculate materials'" [disabled]="saving" (pressed)="calculate(forecast)" /></footer></article>
          } @empty { <p class="empty">{{ 'customerPortal.noForecasts' | hhTranslate: 'No forecasts for the selected tenant.' }}</p> }
        </section>
        @if (selectedForecast) { <section class="section requirements"><div class="section-heading"><div><p class="eyebrow">{{ 'customerPortal.materialPlanning' | hhTranslate: 'Material planning' }}</p><h2>{{ selectedForecast.productSku }}</h2></div></div><div class="table-wrap"><table><thead><tr><th>{{ 'customerPortal.materialSku' | hhTranslate: 'Material' }}</th><th>{{ 'customerPortal.requiredQuantity' | hhTranslate: 'Required' }}</th><th>{{ 'customerPortal.availableQuantity' | hhTranslate: 'Available' }}</th><th>{{ 'customerPortal.shortageQuantity' | hhTranslate: 'Shortage' }}</th></tr></thead><tbody>@for (item of requirements; track item.materialSku) { <tr><td>{{ item.materialSku }}</td><td>{{ item.requiredQuantity | number:'1.0-3' }} {{ item.uom }}</td><td>{{ item.availableQuantity | number:'1.0-3' }}</td><td class="shortage">{{ item.shortageQuantity | number:'1.0-3' }}</td></tr> } @empty { <tr><td colspan="4">{{ 'customerPortal.noRequirements' | hhTranslate: 'No material requirements found.' }}</td></tr> }</tbody></table></div></section> }
      }
      @if (!loading && error) { <hh-state kind="error" [message]="error" (retry)="load()" /> }
    </hh-page-layout>
  `,
  styles: [`:host{font-family:var(--font-sans)}.section{margin-bottom:var(--space-lg)}.section-heading,header,footer{display:flex;align-items:center;justify-content:space-between;gap:var(--space-md)}.eyebrow,.meta{color:var(--text-secondary);font-size:var(--font-size-caption);margin:0}.forecast-form{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-md);padding:var(--space-md);background:var(--surface-muted);border-radius:var(--radius-card)}label{display:grid;gap:var(--space-2xs);color:var(--text-primary);font-size:var(--font-size-caption)}input{border:1px solid var(--border-subtle);border-radius:var(--radius-control);padding:var(--space-sm);background:var(--surface);color:var(--text-primary);font:inherit}.wide{grid-column:1/-1}.actions{display:flex;justify-content:flex-end}.forecast-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:var(--space-md)}.card{padding:var(--space-md);border:1px solid var(--border-subtle);border-radius:var(--radius-card);background:var(--surface);color:var(--text-primary)}.version{padding:var(--space-2xs) var(--space-sm);border-radius:var(--radius-badge);background:var(--surface-muted)}.quantity{font-size:var(--font-size-title);font-weight:700}.action-error,.shortage{color:var(--color-danger)}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse;background:var(--surface);color:var(--text-primary)}th,td{text-align:left;padding:var(--space-sm);border-bottom:1px solid var(--border-subtle)}@media(max-width:700px){.forecast-form{grid-template-columns:1fr}.wide{grid-column:auto}}`],
})
export class ForecastPageComponent implements OnInit {
  private readonly api = inject(ManufacturingApiService); private readonly tenantContext = inject(TenantContextService); private readonly i18n = inject(HisHopeI18nService); private readonly errors = inject(ApiErrorMessageService); private readonly cdr = inject(ChangeDetectorRef); private readonly destroyRef = inject(DestroyRef);
  forecasts: HisHopeSalesForecastDto[] = []; requirements: HisHopeSalesForecastMaterialRequirementDto[] = []; selectedForecast: HisHopeSalesForecastDto | null = null; loading = true; saving = false; error = ""; actionError = ""; tenantLabel: string | null = null;
  draft = { productSku: "", periodStart: new Date().toISOString().slice(0, 10), periodEnd: new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10), quantity: 100, uom: "kg", source: "sales", actor: "planner", version: 1 };
  get pageSubtitle(): string { this.i18n.locale(); return this.i18n.t("customerPortal.tenantScope", "Tenant: {{tenant}}", { tenant: this.tenantLabel ?? "—" }); }
  ngOnInit(): void { this.tenantContext.activeTenantLabel$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((label) => { this.tenantLabel = label; this.cdr.markForCheck(); }); this.tenantContext.activeTenantKey$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load()); }
  load(): void { this.loading = true; this.error = ""; this.api.getSalesForecasts().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.forecasts = items ?? []; this.loading = false; this.cdr.markForCheck(); }, error: (error) => { this.error = this.errors.message(error, "customerPortal.forecastsLoadFailed"); this.loading = false; this.cdr.markForCheck(); } }); }
  create(): void { if (!this.draft.productSku.trim() || !this.draft.uom.trim() || !this.draft.source.trim() || this.draft.quantity <= 0 || !this.draft.periodStart || !this.draft.periodEnd || this.draft.periodStart > this.draft.periodEnd) { this.actionError = this.i18n.t("customerPortal.forecastFormInvalid", "SKU, dates, quantity and UOM are required."); return; } this.saving = true; this.actionError = ""; this.api.createSalesForecast({ ...this.draft, productSku: this.draft.productSku.trim(), uom: this.draft.uom.trim(), source: this.draft.source.trim() }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.load(); this.saving = false; }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.forecastSaveFailed"); this.saving = false; this.cdr.markForCheck(); } }); }
  calculate(forecast: HisHopeSalesForecastDto): void { this.saving = true; this.actionError = ""; this.api.getSalesForecastMaterialRequirements(forecast.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.selectedForecast = forecast; this.requirements = items ?? []; this.saving = false; this.cdr.markForCheck(); }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.requirementsLoadFailed"); this.saving = false; this.cdr.markForCheck(); } }); }
}
