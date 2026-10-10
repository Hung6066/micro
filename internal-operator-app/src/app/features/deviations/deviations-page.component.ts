import { DatePipe, DecimalPipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, OnInit, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { HisHopeActionButtonComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTabsComponent, HisHopeWorkflowStepperComponent , HisHopeSelectComponent} from "@his-hope/frontend-foundation/ui";
import { HisHopeApiErrorMessageService as ApiErrorMessageService, HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeManufacturingDeviationDto, HisHopeProductionBatchDto } from "@his-hope/frontend-foundation/contracts";
import { EntityStatusHistoryPanelComponent } from "../../core/components/entity-status-history-panel.component";
import { ManufacturingApiService } from "../../core/services/manufacturing-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { portalEnumLabel } from "../../core/utils/portal-label.util";
import { buildEntityWorkflowSteps, buildReferenceWorkflowSteps } from "../../core/utils/manufacturing-workflow.util";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, FormsModule, HisHopeActionButtonComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTabsComponent, HisHopeTranslatePipe, HisHopeWorkflowStepperComponent, EntityStatusHistoryPanelComponent, HisHopeSelectComponent],
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'customerPortal.deviationsTitle' | hhTranslate: 'Quality deviations'" [subtitle]="pageSubtitle" />
      <section class="workflow-reference" data-testid="deviation-workflow-reference">
        <h2 class="workflow-reference__title">{{ "customerPortal.workflowDeviation" | hhTranslate: "Deviation lifecycle" }}</h2>
        <hh-workflow-stepper [ariaLabel]="'customerPortal.workflowDeviation' | hhTranslate: 'Deviation lifecycle'" [steps]="referenceWorkflowSteps" />
      </section>
      <hh-tabs label="Deviation sections"><button role="tab" type="button" [attr.aria-selected]="activeTab === 'create'" [class.active]="activeTab === 'create'" (click)="selectTab('create')">{{ 'customerPortal.raiseDeviation' | hhTranslate: 'Raise deviation' }}</button><button role="tab" type="button" [attr.aria-selected]="activeTab === 'list'" [class.active]="activeTab === 'list'" (click)="selectTab('list')">{{ 'customerPortal.deviationGovernance' | hhTranslate: 'Deviation control' }}</button></hh-tabs>
      @if (loading) { <hh-state kind="loading" [message]="'customerPortal.loadingDeviations' | hhTranslate: 'Loading deviations…'" /> }
      @else {
        <section class="section form-section">
          <div class="section-heading"><div><p class="eyebrow">{{ 'customerPortal.deviationGovernance' | hhTranslate: 'Deviation control' }}</p><h2>{{ 'customerPortal.raiseDeviation' | hhTranslate: 'Raise deviation' }}</h2></div><span class="count">{{ deviations.length }}</span></div>
          <form class="deviation-form" (ngSubmit)="create()">
            <label class="wide" for="batchId">{{ 'customerPortal.productionBatch' | hhTranslate: 'Production batch' }}
              <hh-select id="batchId" name="batchId" [(ngModel)]="draft.batchId" required>
                <option value="">{{ 'customerPortal.selectProductionBatch' | hhTranslate: 'Select production batch' }}</option>
                @for (batch of batches; track batch.id) { <option [value]="batch.id">{{ batch.batchNumber }} · {{ batch.plannedQuantity | number:'1.0-2' }} · {{ batchStatusLabel(batch.status) }}</option> }
              </hh-select>
            </label>
            <label>{{ 'customerPortal.deviationType' | hhTranslate: 'Type' }}<input name="type" [(ngModel)]="draft.type" required /></label>
            <label>{{ 'customerPortal.deviationImpact' | hhTranslate: 'Impact' }}<input name="impact" [(ngModel)]="draft.impact" required /></label>
            <label>{{ 'customerPortal.requestedBy' | hhTranslate: 'Requested by' }}<input name="requestedBy" [(ngModel)]="draft.requestedBy" required /></label>
            <label class="wide">{{ 'customerPortal.deviationDescription' | hhTranslate: 'Description' }}<textarea name="description" rows="3" [(ngModel)]="draft.description" required></textarea></label>
            <div class="wide actions"><hh-action-button type="submit" kind="primary" icon="report_problem" [label]="'customerPortal.raiseDeviation' | hhTranslate: 'Raise deviation'" [disabled]="saving" /></div>
          </form>
          @if (actionError) { <p class="action-error" role="alert">{{ actionError }}</p> }
        </section>
        <section class="section deviation-grid">
          @for (deviation of deviations; track deviation.id) {
            <article class="card"><header><div><strong>{{ deviation.type }}</strong><p class="meta">{{ batchLabel(deviation.productionBatchId) }}</p></div><span class="status" [class.approved]="deviation.status === 'Approved'">{{ deviationStatusLabel(deviation.status) }}</span></header><hh-workflow-stepper class="entity-workflow" [attr.data-testid]="'deviation-workflow-' + deviation.id" [ariaLabel]="'customerPortal.workflowDeviation' | hhTranslate: 'Deviation lifecycle'" [steps]="deviationWorkflowSteps(deviation.status)" /><app-entity-status-history-panel [entityId]="deviation.id" [loadHistory]="loadDeviationStatusHistory" [statusLabel]="deviationStatusLabelFn" /><p>{{ deviation.description }}</p><p class="meta">{{ deviation.impact }} · {{ deviation.requestedBy }} · {{ deviation.createdAt | date: 'medium' }}</p><footer><div class="actions">@if (deviation.status === 'Requested') { <hh-action-button kind="primary" icon="check" [label]="'customerPortal.approveDeviation' | hhTranslate: 'Approve'" [disabled]="saving" (pressed)="change(deviation, 'approve')" /><hh-action-button kind="secondary" icon="close" [label]="'customerPortal.rejectDeviation' | hhTranslate: 'Reject'" [disabled]="saving" (pressed)="change(deviation, 'reject')" /> } @if (deviation.status === 'Approved') { <hh-action-button kind="secondary" icon="done_all" [label]="'customerPortal.closeDeviation' | hhTranslate: 'Close'" [disabled]="saving" (pressed)="change(deviation, 'close')" /> }</div></footer></article>
          } @empty { <p class="empty">{{ 'customerPortal.noDeviations' | hhTranslate: 'No deviations for the selected tenant.' }}</p> }
        </section>
      }
      @if (!loading && error) { <hh-state kind="error" [message]="error" (retry)="load()" /> }
    </hh-page-layout>
  `,
  styles: [`:host{font-family:var(--font-sans)}.section{margin-bottom:var(--space-lg)}.section-heading,header,footer{display:flex;align-items:center;justify-content:space-between;gap:var(--space-md)}.eyebrow,.meta{color:var(--text-secondary);font-size:var(--font-size-caption);margin:0}.count{font-size:var(--font-size-title);font-weight:700}.workflow-reference{margin-bottom:var(--space-lg);padding:var(--space-md);border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-subtle)}.workflow-reference__title{margin:0 0 var(--space-sm);font-size:var(--font-size-caption);font-weight:var(--font-weight-semibold);color:var(--text-secondary)}.entity-workflow{margin:var(--space-sm) 0;overflow-x:auto}.deviation-form{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-md);padding:var(--space-md);background:var(--surface-muted);border-radius:var(--radius-card)}label{display:grid;gap:var(--space-2xs);color:var(--text-primary);font-size:var(--font-size-caption)}input,select,textarea{border:1px solid var(--border-subtle);border-radius:var(--radius-control);padding:var(--space-sm);background:var(--surface);color:var(--text-primary);font:inherit}.wide{grid-column:1/-1}.actions{display:flex;flex-wrap:wrap;gap:var(--space-sm);justify-content:flex-end}.deviation-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:var(--space-md)}.card{padding:var(--space-md);border:1px solid var(--border-subtle);border-radius:var(--radius-card);background:var(--surface);color:var(--text-primary)}.status{padding:var(--space-2xs) var(--space-sm);border-radius:var(--radius-badge);background:var(--surface-muted);font-size:var(--font-size-caption)}.status.approved{background:var(--color-success-subtle);color:var(--color-success)}.action-error{color:var(--color-danger)}@media(max-width:700px){.deviation-form{grid-template-columns:1fr}.wide{grid-column:auto}}`],
})
export class DeviationsPageComponent implements OnInit, AfterViewInit {
  activeTab = "create";
  selectTab(tab: string): void { this.activeTab = tab; this.applyTabVisibility(); this.cdr.markForCheck(); }
  ngAfterViewInit(): void { const observer = new MutationObserver(() => { if (document.querySelectorAll("section.section").length) { this.applyTabVisibility(); observer.disconnect(); } }); observer.observe(document.body, { childList: true, subtree: true }); this.applyTabVisibility(); }
  private applyTabVisibility(): void { const sections = Array.from(document.querySelectorAll<HTMLElement>("section.section")); sections.forEach((section, index) => section.hidden = index !== (this.activeTab === "create" ? 0 : 1)); }
  private readonly api = inject(ManufacturingApiService); private readonly tenantContext = inject(TenantContextService); private readonly i18n = inject(HisHopeI18nService); private readonly errors = inject(ApiErrorMessageService); private readonly cdr = inject(ChangeDetectorRef); private readonly destroyRef = inject(DestroyRef);
  deviations: HisHopeManufacturingDeviationDto[] = []; batches: HisHopeProductionBatchDto[] = []; loading = true; saving = false; error = ""; actionError = ""; tenantLabel: string | null = null; actor = "operator";
  draft = { batchId: "", type: "Quality", description: "", impact: "" , requestedBy: "operator" };
  get pageSubtitle(): string { this.i18n.locale(); return this.i18n.t("customerPortal.tenantScope", "Tenant: {{tenant}}", { tenant: this.tenantLabel ?? "—" }); }
  batchLabel(batchId: string): string { const batch = this.batches.find((item) => item.id === batchId); return batch ? `Batch ${batch.batchNumber} · ${this.batchStatusLabel(batch.status)}` : "Batch"; }
  batchStatusLabel(status: string): string { return portalEnumLabel(this.i18n, "productionBatchStatus", status); }
  deviationStatusLabel(status: string): string { return portalEnumLabel(this.i18n, "deviationStatus", status); }
  readonly loadDeviationStatusHistory = (deviationId: string) => this.api.getDeviationStatusHistory(deviationId);
  readonly deviationStatusLabelFn = (status: string) => this.deviationStatusLabel(status);
  get referenceWorkflowSteps() { return buildReferenceWorkflowSteps(this.i18n, "deviation"); }
  deviationWorkflowSteps(status: string) { return buildEntityWorkflowSteps(this.i18n, "deviation", status); }
  ngOnInit(): void { this.tenantContext.activeTenantLabel$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((label) => { this.tenantLabel = label; this.cdr.markForCheck(); }); this.tenantContext.activeTenantKey$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.load()); }
  load(): void { this.loading = true; this.error = ""; this.api.getProductionBatches().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (batches) => { this.batches = batches ?? []; this.api.getDeviations().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: (items) => { this.deviations = items ?? []; this.loading = false; this.cdr.markForCheck(); }, error: (error) => { this.error = this.errors.message(error, "customerPortal.deviationsLoadFailed"); this.loading = false; this.cdr.markForCheck(); } }); }, error: (error) => { this.error = this.errors.message(error, "customerPortal.batchesLoadFailed"); this.loading = false; this.cdr.markForCheck(); } }); }
  create(): void { if (!this.draft.batchId.trim() || !this.draft.description.trim() || !this.draft.impact.trim() || !this.draft.requestedBy.trim()) { this.actionError = this.i18n.t("customerPortal.deviationFormInvalid", "Batch, type, description, impact and requester are required."); return; } this.saving = true; this.actionError = ""; this.api.createDeviation(this.draft.batchId.trim(), { type: this.draft.type, description: this.draft.description, impact: this.draft.impact, requestedBy: this.draft.requestedBy }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.draft = { batchId: "", type: "Quality", description: "", impact: "", requestedBy: this.actor }; this.load(); this.saving = false; }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.deviationSaveFailed"); this.saving = false; this.cdr.markForCheck(); } }); }
  change(deviation: HisHopeManufacturingDeviationDto, action: "approve" | "reject" | "close"): void { this.saving = true; this.actionError = ""; const request = action === "approve" ? this.api.approveDeviation(deviation.id, this.actor) : action === "reject" ? this.api.rejectDeviation(deviation.id, this.actor) : this.api.closeDeviation(deviation.id, this.actor); request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.load(); this.saving = false; }, error: (error) => { this.actionError = this.errors.message(error, "customerPortal.deviationActionFailed"); this.saving = false; this.cdr.markForCheck(); } }); }
}
