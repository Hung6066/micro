import { ChangeDetectorRef, Component, effect, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { catchError, of } from "rxjs";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import { OperatorMobileApiService, type ManufacturingException, type ManufacturingOee, type ManufacturingProductionCost, type ManufacturingSummary, type ProductionBatch, type ProductionKpi } from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { manufacturingEnumLabel } from "../../core/manufacturing-enum-label.util";

@Component({ standalone: true, imports: [FormsModule, HisHopeTranslatePipe], templateUrl: "./production-work-page.component.html", styleUrls: ["./production-work-page.component.scss"] })
export class ProductionWorkPageComponent {
  private readonly api = inject(OperatorMobileApiService);
  private readonly queue = inject(OperationQueueService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  batches: ProductionBatch[] = [];
  selectedBatchId = "";
  processStep = "operation";
  inputQuantity = 0;
  outputQuantity = 0;
  qcStatus = "Pending";
  message = "";
  loadError = "";
  summary: ManufacturingSummary | null = null;
  kpis: ProductionKpi | null = null;
  oee: ManufacturingOee | null = null;
  exceptions: ManufacturingException[] = [];
  costs: ManufacturingProductionCost | null = null;

  batchStatusLabel(status: string): string { return manufacturingEnumLabel(this.i18n, "productionBatchStatus", status); }
  dataCompletenessLabel(value: string | null | undefined): string { return manufacturingEnumLabel(this.i18n, "dataCompleteness", value); }
  formatNumber(value: number): string { return this.i18n.formatNumber(value); }
  formatPercent(value: number): string { return this.i18n.formatNumber(value, { maximumFractionDigits: 2 }) + "%"; }
  formatCurrency(value: number): string { return this.i18n.formatCurrency(value); }
  qcStatusLabel(status: string): string { return manufacturingEnumLabel(this.i18n, "qualityInspectionStatus", status); }
  oeeStatusLabel(status: string): string { return manufacturingEnumLabel(this.i18n, "oeeStatus", status); }
  oeeMetricLabel(metric: string): string { return manufacturingEnumLabel(this.i18n, "oeeMissingMetrics", metric); }
  oeeMissingMetricsLabel(metrics: string[]): string { return metrics.map((metric) => this.oeeMetricLabel(metric)).join(", "); }
  exceptionSeverityLabel(severity: string): string { return manufacturingEnumLabel(this.i18n, "exceptionSeverity", severity); }
  exceptionCodeLabel(code: string): string { return manufacturingEnumLabel(this.i18n, "exceptionCode", code); }

  constructor() {
    effect(() => {
      if (!this.tenant.activeTenantKey()) {
        this.batches = [];
        this.summary = null;
        this.kpis = null;
        this.oee = null;
        this.exceptions = [];
        this.costs = null;
        this.selectedBatchId = "";
        return;
      }
      this.api.getProductionBatches("Started").pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of([]); })).subscribe((batches) => {
        setTimeout(() => {
          this.batches = batches;
          if (this.selectedBatchId && !batches.some((batch) => batch.id === this.selectedBatchId)) this.selectedBatchId = "";
          this.cdr.markForCheck();
        });
      });
      this.api.getManufacturingSummary().pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of(null); })).subscribe((summary) => {
        setTimeout(() => { this.summary = summary; this.cdr.markForCheck(); });
      });
      this.api.getProductionKpis().pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of(null); })).subscribe((kpis) => {
        setTimeout(() => { this.kpis = kpis; this.cdr.markForCheck(); });
      });
      this.api.getOee().pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of(null); })).subscribe((oee) => {
        setTimeout(() => { this.oee = oee; this.cdr.markForCheck(); });
      });
      this.api.getExceptions().pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of([]); })).subscribe((exceptions) => {
        setTimeout(() => { this.exceptions = exceptions.slice(0, 5); this.cdr.markForCheck(); });
      });
      this.api.getProductionCosts().pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of(null); })).subscribe((costs) => {
        setTimeout(() => { this.costs = costs; this.cdr.markForCheck(); });
      });
    });
  }

  async recordOperation(): Promise<void> {
    const selectedBatch = this.batches.find((batch) => batch.id === this.selectedBatchId);
    if (!selectedBatch || this.inputQuantity <= 0 || this.outputQuantity <= 0) {
      this.message = this.i18n.t("mobile.operatorProductionValidation", "Select a batch and enter a positive output quantity.");
      return;
    }
    const scope = this.tenant.commandScope;
    if (!scope) {
      this.message = this.i18n.t("mobile.operatorTenantRequired", "Sign in and select an operational tenant before recording work.");
      return;
    }
    const operation = await this.queue.submit(
      { ...scope, endpoint: `/production-batches/${selectedBatch.id}/operations`, expectedVersion: selectedBatch.version, payload: { sequence: 1, processStep: this.processStep.trim() || "operation", operator: scope.subjectId, inputQuantity: this.inputQuantity, outputQuantity: this.outputQuantity, required: true, qcStatus: this.qcStatus, tenantKey: scope.tenantKey } },
      (queued) => this.api.recordProductionOperation(queued),
    );
    this.message = operation.status === "synced" ? this.i18n.t("mobile.operatorOperationRecorded", "Operation recorded.") : this.i18n.t("mobile.operatorPendingSync", "Pending sync — it will retry when connected.");
  }
}
