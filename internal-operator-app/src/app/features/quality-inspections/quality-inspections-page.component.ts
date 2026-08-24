import { DatePipe, DecimalPipe } from "@angular/common";
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { HisHopeLotDto, HisHopeQualityInspectionDto } from "@his-hope/frontend-foundation/contracts";
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
      <hh-page-header hhPageHeader [title]="'customerPortal.inspectionsTitle' | hhTranslate: 'Quality inspections'" [subtitle]="pageSubtitle" />
      @if (loading) { <hh-state kind="loading" [message]="'customerPortal.loadingInspections' | hhTranslate: 'Loading quality data…'" /> }
      @else {
        <section class="section form-section">
          <div class="section-heading"><div><p class="eyebrow">{{ 'customerPortal.qualityGovernance' | hhTranslate: 'Quality control' }}</p><h2>{{ 'customerPortal.createInspection' | hhTranslate: 'Record inspection' }}</h2></div></div>
          <form class="inspection-form" (ngSubmit)="create()">
            <label class="wide">{{ 'customerPortal.inspectionLot' | hhTranslate: 'Lot' }}<select name="lotId" [(ngModel)]="draft.lotId" required><option value="">{{ 'customerPortal.selectLot' | hhTranslate: 'Select a lot' }}</option>@for (lot of lots; track lot.id) { <option [value]="lot.id">{{ lot.sku }} · {{ lot.quantity | number:'1.0-3' }} {{ lot.uom }} · {{ lot.disposition }}</option> }</select></label>
            <label>{{ 'customerPortal.inspectionStatus' | hhTranslate: 'Status' }}<select name="status" [(ngModel)]="draft.status"><option value="Pending">Pending</option><option value="Pass">Pass</option><option value="Fail">Fail</option></select></label>
            <label>{{ 'customerPortal.moisturePercent' | hhTranslate: 'Moisture %' }}<input name="moisture" type="number" min="0" max="100" step="0.01" [(ngModel)]="draft.moisturePercent" required /></label>
            <label>{{ 'customerPortal.inspector' | hhTranslate: 'Inspector' }}<input name="inspector" [(ngModel)]="draft.inspector" required /></label>
            <label class="wide">{{ 'customerPortal.inspectionNotes' | hhTranslate: 'Notes' }}<textarea name="notes" rows="2" [(ngModel)]="draft.notes"></textarea></label>
            <div class="wide actions"><hh-action-button type="submit" kind="primary" icon="fact_check" [label]="'customerPortal.createInspection' | hhTranslate: 'Record inspection'" [disabled]="saving" /></div>
          </form>
          @if (actionError) { <p class="action-error" role="alert">{{ actionError }}</p> }
        </section>
        <section class="section"><div class="section-heading"><div><p class="eyebrow">{{ 'customerPortal.inspectionHistory' | hhTranslate: 'Inspection history' }}</p><h2>{{ selectedLot?.sku ?? ('customerPortal.selectLot' | hhTranslate: 'Select a lot') }}</h2></div><select class="history-select" [ngModel]="selectedLot?.id ?? ''" (ngModelChange)="selectLot($event)"><option value="">{{ 'customerPortal.selectLot' | hhTranslate: 'Select a lot' }}</option>@for (lot of lots; track lot.id) { <option [value]="lot.id">{{ lot.sku }} · {{ lot.quantity | number:'1.0-3' }} {{ lot.uom }}</option> }</select></div>
          @if (selectedLot && inspections.length) { <div class="inspection-grid">@for (inspection of inspections; track inspection.id) { <article class="card"><header><strong>{{ inspection.status }}</strong><span class="meta">{{ inspection.inspectedAt | date:'medium' }}</span></header><p>{{ inspection.moisturePercent | number:'1.0-2' }}% · {{ inspection.inspector }}</p>@if (inspection.notes) { <p class="meta">{{ inspection.notes }}</p> }</article> }</div> } @else { <p class="empty">{{ 'customerPortal.noInspections' | hhTranslate: 'No inspections for this lot.' }}</p> }
        </section>
      }
      @if (!loading && error) { <hh-state kind="error" [message]="error" (retry)="load()" /> }
    </hh-page-layout>
  `,
  styles: [`:host{font-family:var(--font-sans)}.section{margin-bottom:var(--space-lg)}.section-heading,header{display:flex;align-items:center;justify-content:space-between;gap:var(--space-md)}.eyebrow,.meta{color:var(--text-secondary);font-size:var(--font-size-caption);margin:0}.inspection-form{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-md);padding:var(--space-md);background:var(--surface-muted);border-radius:var(--radius-card)}label{display:grid;gap:var(--space-2xs);color:var(--text-primary);font-size:var(--font-size-caption)}input,select,textarea{border:1px solid var(--border-subtle);border-radius:var(--radius-control);padding:var(--space-sm);background:var(--surface);color:var(--text-primary);font:inherit}.wide{grid-column:1/-1}.actions{display:flex;justify-content:flex-end}.history-select{min-width:220px}.inspection-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:var(--space-md)}.card{padding:var(--space-md);border:1px solid var(--border-subtle);border-radius:var(--radius-card);background:var(--surface);color:var(--text-primary)}.action-error{color:var(--color-danger)}@media(max-width:700px){.inspection-form{grid-template-columns:1fr}.wide{grid-column:auto}.section-heading{align-items:flex-start;flex-direction:column}.history-select{width:100%}}`],
})
export class QualityInspectionsPageComponent implements OnInit {
  private readonly api = inject(ManufacturingApiService); private readonly tenantContext = inject(TenantContextService); private readonly i18n = inject(HisHopeI18nService); private readonly errors = inject(ApiErrorMessageService); private readonly cdr = inject(ChangeDetectorRef); private readonly destroyRef = inject(DestroyRef);
  lots: HisHopeLotDto[] = []; inspections: HisHopeQualityInspectionDto[] = []; selectedLot: HisHopeLotDto | null = null; loading = true; saving = false; error = ""; actionError = ""; tenantLabel: string | null = null;
  draft = { lotId: "", status: "Pending", moisturePercent: 0, inspector: "operator", notes: "" };
  get pageSubtitle(): string { this.i18n.locale(); return this.i18n.t("customerPortal.tenantScope", "Tenant: {{tenant}}", { tenant: this.tenantLabel ?? "—" }); }
  ngOnInit(): void { this.tenantContext.activeTenantLabel$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((label) => { this.tenantLabel = label; this.cdr.markForCheck(); }); this.tenantContext.activeTenantKey$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load()); }
  load(): void { this.loading = true; this.error = ""; this.api.getLots({ limit: 100 }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (lots) => { this.lots = lots ?? []; this.selectedLot = this.lots.find((lot) => lot.id === this.draft.lotId) ?? this.lots[0] ?? null; this.draft.lotId = this.selectedLot?.id ?? ""; this.loading = false; if (this.selectedLot) this.loadInspections(this.selectedLot.id); else this.cdr.markForCheck(); }, error: (error) => { this.error = this.errors.message(error, "customerPortal.inspectionsLoadFailed"); this.loading = false; this.cdr.markForCheck(); } }); }
  selectLot(lotId: string): void { this.selectedLot = this.lots.find((lot) => lot.id === lotId) ?? null; this.draft.lotId = lotId; if (lotId) this.loadInspections(lotId); else { this.inspections = []; this.cdr.markForCheck(); } }
  loadInspections(lotId: string): void { this.api.getLotQualityInspections(lotId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.inspections = items ?? []; this.cdr.markForCheck(); }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.inspectionsLoadFailed"); this.cdr.markForCheck(); } }); }
  create(): void { if (!this.draft.lotId || !this.draft.inspector.trim() || this.draft.moisturePercent < 0 || this.draft.moisturePercent > 100) { this.actionError = this.i18n.t("customerPortal.inspectionFormInvalid", "Lot, inspector and moisture from 0 to 100 are required."); return; } const tenantKey = this.tenantContext.getActiveTenantKey(); if (!tenantKey) { this.actionError = this.i18n.t("customerPortal.tenantRequired", "Select a tenant first."); return; } this.saving = true; this.actionError = ""; this.api.createQualityInspection({ lotId: this.draft.lotId, tenantKey, status: this.draft.status, moisturePercent: this.draft.moisturePercent, inspector: this.draft.inspector.trim(), notes: this.draft.notes.trim() || undefined }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.saving = false; this.loadInspections(this.draft.lotId); }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.inspectionSaveFailed"); this.saving = false; this.cdr.markForCheck(); } }); }
}
