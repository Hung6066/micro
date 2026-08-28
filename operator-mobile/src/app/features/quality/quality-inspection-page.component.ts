import { ChangeDetectorRef, Component, effect, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import { OperatorMobileApiService, type InspectionPlanVersion, type QualityInspection } from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import { HisHopeI18nService, HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { catchError, of } from "rxjs";
import type { LotSummary } from "../../core/services/operator-mobile-api.service";
import { manufacturingEnumLabel } from "../../core/manufacturing-enum-label.util";

@Component({ standalone: true, imports: [FormsModule, HisHopeTranslatePipe], templateUrl: "./quality-inspection-page.component.html", styleUrls: ["./quality-inspection-page.component.scss"] })
export class QualityInspectionPageComponent {
  private readonly api = inject(OperatorMobileApiService);
  private readonly queue = inject(OperationQueueService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  lotId = "";
  inspector = "";
  moisturePercent = 0;
  status = "Pass";
  testCode = "";
  testName = "";
  measuredValue = 0;
  testUom = "";
  testResult = "Pass";
  testMethod = "";
  evidenceReference = "";
  message = "";
  loadError = "";
  lots: LotSummary[] = [];
  planVersions: InspectionPlanVersion[] = [];
  inspectionPlanVersionId = "";
  inspections: QualityInspection[] = [];
  inspectionId = "";
  sampleCode = "";
  sampleLocation = "";
  sampleNotes = "";

  constructor() {
    effect(() => {
      const tenantKey = this.tenant.activeTenantKey?.();
      if (!tenantKey) { this.lots = []; this.planVersions = []; this.inspectionPlanVersionId = ""; this.inspections = []; this.inspectionId = ""; return; }
      this.api.getLots().pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of([]); })).subscribe((lots) => {
        setTimeout(() => {
          this.lots = lots;
          if (this.lotId && !lots.some((lot) => lot.id === this.lotId)) this.lotId = "";
          this.cdr.markForCheck();
        });
      });
      this.api.getInspectionPlanVersions().pipe(catchError(() => of([]))).subscribe((plans) => {
        setTimeout(() => { this.planVersions = plans; this.cdr.markForCheck(); });
      });
    });
  }

  loadInspections(): void {
    if (!this.lotId) { this.inspections = []; this.inspectionId = ""; return; }
    this.api.getLotQualityInspections(this.lotId).pipe(catchError(() => of([]))).subscribe((inspections) => {
      setTimeout(() => { this.inspections = inspections; this.inspectionId = inspections[0]?.id ?? ""; this.cdr.markForCheck(); });
    });
  }

  lotOptionLabel(lot: LotSummary): string {
    const expiry = lot.bestBefore ? ` · ${this.i18n.formatDate(lot.bestBefore)}` : "";
    return `${lot.lotCode || lot.sku} · ${lot.quantity} ${lot.uom || ""}${expiry}`;
  }

  inspectionStatusLabel(status: string): string {
    return manufacturingEnumLabel(this.i18n, "qualityInspectionStatus", status);
  }

  async submitInspection(): Promise<void> {
    if (!this.lotId.trim() || !this.inspector.trim()) {
      this.message = this.i18n.t("mobile.operatorQualityValidation", "Lot and inspector are required.");
      return;
    }
    const scope = this.tenant.commandScope;
    if (!scope) {
      this.message = this.i18n.t("mobile.operatorTenantRequired", "Sign in and select an operational tenant before saving an inspection.");
      return;
    }
    const operation = await this.queue.submit(
      { ...scope, endpoint: "/quality-inspections", payload: { lotId: this.lotId.trim(), status: this.status, moisturePercent: this.moisturePercent, inspector: this.inspector.trim(), inspectionPlanVersionId: this.inspectionPlanVersionId || undefined, results: this.testCode.trim() ? [{ testCode: this.testCode.trim(), testName: this.testName.trim() || this.testCode.trim(), measuredValue: this.measuredValue, uom: this.testUom.trim() || "%", result: this.testResult, method: this.testMethod.trim() || undefined, evidenceReference: this.evidenceReference.trim() || undefined }] : undefined } },
      (queued) => this.api.createQualityInspection(queued),
    );
    this.message = operation.status === "synced" ? this.i18n.t("mobile.operatorInspectionSaved", "Inspection saved.") : this.i18n.t("mobile.operatorPendingSync", "Pending sync — it will retry when connected.");
  }

  async recordSample(): Promise<void> {
    if (!this.inspectionId || !this.sampleCode.trim() || !this.inspector.trim()) {
      this.message = this.i18n.t("mobile.operatorSampleValidation", "Select an inspection and enter a sample code and collector.");
      return;
    }
    const scope = this.tenant.commandScope;
    if (!scope) {
      this.message = this.i18n.t("mobile.operatorTenantRequired", "Sign in and select an operational tenant before saving a sample.");
      return;
    }
    const operation = await this.queue.submit(
      { ...scope, endpoint: "/quality-samples", payload: { inspectionId: this.inspectionId, sampleCode: this.sampleCode.trim(), collectedBy: this.inspector.trim(), location: this.sampleLocation.trim() || undefined, notes: this.sampleNotes.trim() || undefined } },
      (queued) => this.api.createQualitySample(queued),
    );
    this.message = operation.status === "synced" ? this.i18n.t("mobile.operatorSampleSaved", "Quality sample saved.") : this.i18n.t("mobile.operatorPendingSync", "Pending sync — it will retry when connected.");
  }
}
