import { ChangeDetectorRef, Component, effect, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import {
  OperatorMobileApiService,
  type InspectionPlanVersion,
  type ProductionBatch,
  type QualityInspection,
  type QualitySample,
  type ManufacturingDeviation,
} from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { catchError, of } from "rxjs";
import type { LotSummary } from "../../core/services/operator-mobile-api.service";
import { manufacturingEnumLabel } from "../../core/manufacturing-enum-label.util";
import {
  HisHopeSelectComponent,
  HisHopeTabsComponent,
} from "@his-hope/frontend-foundation/ui";
import { operatorMobileErrorMessage } from "../../core/operator-mobile-error.util";
import { NativeCapabilityService } from "../../core/native-capability.service";

@Component({
  standalone: true,
  imports: [
    FormsModule,
    HisHopeTranslatePipe,
    HisHopeSelectComponent,
    HisHopeTabsComponent,
  ],
  templateUrl: "./quality-inspection-page.component.html",
  styleUrls: ["./quality-inspection-page.component.scss"],
})
export class QualityInspectionPageComponent {
  activeTab: "inspection" | "sample" | "deviation" = "inspection";
  private readonly api = inject(OperatorMobileApiService);
  private readonly queue = inject(OperationQueueService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly native = inject(NativeCapabilityService);
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
  samples: QualitySample[] = [];
  sampleId = "";
  sampleDisposition = "Pending";
  sampleDispositionReason = "";
  batches: ProductionBatch[] = [];
  deviationBatchId = "";
  deviationType = "Process deviation";
  deviationDescription = "";
  deviationImpact = "";
  deviations: ManufacturingDeviation[] = [];
  deviationId = "";
  deviationReviewStatus: "Approved" | "Rejected" | "Closed" = "Approved";
  deviationReviewer = "";
  deviationReviewNotes = "";

  async captureEvidence(): Promise<void> {
    const photo = await this.native.capturePhoto({
      quality: 82,
      width: 1600,
      height: 1600,
    });
    if (photo?.uri) this.evidenceReference = photo.uri;
  }

  constructor() {
    effect(() => {
      const tenantKey = this.tenant.activeTenantKey?.();
      if (!tenantKey) {
        this.lots = [];
        this.planVersions = [];
        this.inspectionPlanVersionId = "";
        this.inspections = [];
        this.inspectionId = "";
        return;
      }
      this.api
        .getLots()
        .pipe(
          catchError((error) => {
            this.loadError = operatorMobileErrorMessage(this.i18n, error);
            this.cdr.markForCheck();
            return of([]);
          }),
        )
        .subscribe((lots) => {
          setTimeout(() => {
            this.lots = lots;
            if (this.lotId && !lots.some((lot) => lot.id === this.lotId))
              this.lotId = "";
            this.cdr.markForCheck();
          });
        });
      this.api
        .getInspectionPlanVersions()
        .pipe(catchError(() => of([])))
        .subscribe((plans) => {
          setTimeout(() => {
            this.planVersions = plans;
            this.cdr.markForCheck();
          });
        });
      this.api
        .getProductionBatches("Started")
        .pipe(catchError(() => of([])))
        .subscribe((batches) => {
          setTimeout(() => {
            this.batches = batches;
            if (
              this.deviationBatchId &&
              !batches.some((batch) => batch.id === this.deviationBatchId)
            )
              this.deviationBatchId = "";
            this.cdr.markForCheck();
          });
        });
      this.api
        .getDeviations(undefined, "Open")
        .pipe(catchError(() => of([])))
        .subscribe((deviations) => {
          setTimeout(() => {
            this.deviations = deviations;
            if (
              this.deviationId &&
              !deviations.some((deviation) => deviation.id === this.deviationId)
            )
              this.deviationId = "";
            this.cdr.markForCheck();
          });
        });
    });
  }

  loadInspections(): void {
    if (!this.lotId) {
      this.inspections = [];
      this.inspectionId = "";
      this.samples = [];
      this.sampleId = "";
      return;
    }
    this.api
      .getLotQualityInspections(this.lotId)
      .pipe(catchError(() => of([])))
      .subscribe((inspections) => {
        setTimeout(() => {
          this.inspections = inspections;
          this.inspectionId = inspections[0]?.id ?? "";
          this.loadSamples();
          this.cdr.markForCheck();
        });
      });
  }

  loadSamples(): void {
    if (!this.inspectionId) {
      this.samples = [];
      this.sampleId = "";
      return;
    }
    this.api
      .getQualitySamples(this.inspectionId)
      .pipe(catchError(() => of([])))
      .subscribe((samples) => {
        setTimeout(() => {
          this.samples = samples;
          this.sampleId = samples[0]?.id ?? "";
          this.sampleDisposition = samples[0]?.disposition ?? "Pending";
          this.cdr.markForCheck();
        });
      });
  }

  selectSample(): void {
    const sample = this.samples.find((item) => item.id === this.sampleId);
    this.sampleDisposition = sample?.disposition ?? "Pending";
  }

  lotOptionLabel(lot: LotSummary): string {
    const expiry = lot.bestBefore
      ? ` · ${this.i18n.formatDate(lot.bestBefore)}`
      : "";
    return `${lot.lotCode || lot.sku} · ${lot.quantity} ${lot.uom || ""}${expiry}`;
  }

  selectedLot(): LotSummary | undefined {
    return this.lots.find((lot) => lot.id === this.lotId);
  }

  inspectionStatusLabel(status: string): string {
    return manufacturingEnumLabel(this.i18n, "qualityInspectionStatus", status);
  }

  async submitInspection(): Promise<void> {
    if (!this.lotId.trim() || !this.inspector.trim()) {
      this.message = this.i18n.t(
        "mobile.operatorQualityValidation",
        "Lot and inspector are required.",
      );
      return;
    }
    const scope = this.tenant.commandScope;
    if (!scope) {
      this.message = this.i18n.t(
        "mobile.operatorTenantRequired",
        "Sign in and select an operational tenant before saving an inspection.",
      );
      return;
    }
    const operation = await this.queue.submit(
      {
        ...scope,
        endpoint: "/quality-inspections",
        payload: {
          lotId: this.lotId.trim(),
          status: this.status,
          moisturePercent: this.moisturePercent,
          inspector: this.inspector.trim(),
          inspectionPlanVersionId: this.inspectionPlanVersionId || undefined,
          results: this.testCode.trim()
            ? [
                {
                  testCode: this.testCode.trim(),
                  testName: this.testName.trim() || this.testCode.trim(),
                  measuredValue: this.measuredValue,
                  uom: this.testUom.trim() || "%",
                  result: this.testResult,
                  method: this.testMethod.trim() || undefined,
                  evidenceReference: this.evidenceReference.trim() || undefined,
                },
              ]
            : undefined,
        },
      },
      (queued) => this.api.createQualityInspection(queued),
    );
    this.message =
      operation.status === "synced"
        ? this.i18n.t("mobile.operatorInspectionSaved", "Inspection saved.")
        : this.i18n.t(
            "mobile.operatorPendingSync",
            "Pending sync — it will retry when connected.",
          );
  }

  async recordSample(): Promise<void> {
    if (
      !this.inspectionId ||
      !this.sampleCode.trim() ||
      !this.inspector.trim()
    ) {
      this.message = this.i18n.t(
        "mobile.operatorSampleValidation",
        "Select an inspection and enter a sample code and collector.",
      );
      return;
    }
    const scope = this.tenant.commandScope;
    if (!scope) {
      this.message = this.i18n.t(
        "mobile.operatorTenantRequired",
        "Sign in and select an operational tenant before saving a sample.",
      );
      return;
    }
    const operation = await this.queue.submit(
      {
        ...scope,
        endpoint: "/quality-samples",
        payload: {
          inspectionId: this.inspectionId,
          sampleCode: this.sampleCode.trim(),
          collectedBy: this.inspector.trim(),
          location: this.sampleLocation.trim() || undefined,
          notes: this.sampleNotes.trim() || undefined,
        },
      },
      (queued) => this.api.createQualitySample(queued),
    );
    this.message =
      operation.status === "synced"
        ? this.i18n.t("mobile.operatorSampleSaved", "Quality sample saved.")
        : this.i18n.t(
            "mobile.operatorPendingSync",
            "Pending sync — it will retry when connected.",
          );
  }

  async changeSampleDisposition(): Promise<void> {
    const scope = this.tenant.commandScope;
    if (!scope || !this.sampleId || this.sampleDisposition === "Pending") {
      this.message = this.i18n.t(
        "mobile.operatorSampleDispositionValidation",
        "Select a sample and a final disposition.",
      );
      return;
    }
    const operation = await this.queue.submit(
      {
        ...scope,
        endpoint: `/quality-samples/${this.sampleId}/disposition`,
        payload: {
          disposition: this.sampleDisposition,
          actor: this.inspector.trim(),
          reason: this.sampleDispositionReason.trim() || undefined,
        },
      },
      (queued) => this.api.changeQualitySampleDisposition(queued),
    );
    this.message =
      operation.status === "synced"
        ? this.i18n.t(
            "mobile.operatorSampleDispositionSaved",
            "Sample disposition saved.",
          )
        : this.i18n.t(
            "mobile.operatorPendingSync",
            "Pending sync — it will retry when connected.",
          );
  }

  async createDeviation(): Promise<void> {
    const scope = this.tenant.commandScope;
    if (
      !scope ||
      !this.deviationBatchId ||
      !this.deviationType.trim() ||
      !this.deviationDescription.trim() ||
      !this.deviationImpact.trim()
    ) {
      this.message = this.i18n.t(
        "mobile.operatorDeviationValidation",
        "Select a batch and complete the deviation details.",
      );
      return;
    }
    const operation = await this.queue.submit(
      {
        ...scope,
        endpoint: `/production-batches/${this.deviationBatchId}/deviations`,
        payload: {
          type: this.deviationType.trim(),
          description: this.deviationDescription.trim(),
          impact: this.deviationImpact.trim(),
          requestedBy: scope.subjectId,
        },
      },
      (queued) => this.api.createDeviation(queued),
    );
    this.message =
      operation.status === "synced"
        ? this.i18n.t("mobile.operatorDeviationSaved", "Deviation submitted.")
        : this.i18n.t(
            "mobile.operatorPendingSync",
            "Pending sync — it will retry when connected.",
          );
  }

  async reviewDeviation(): Promise<void> {
    if (!this.deviationId || !this.deviationReviewer.trim()) {
      this.message = this.i18n.t(
        "mobile.operatorDeviationReviewValidation",
        "Select a deviation and enter the reviewing operator.",
      );
      return;
    }
    const result = await this.api.changeDeviationStatus(
      this.deviationId,
      this.deviationReviewStatus,
      this.deviationReviewer.trim(),
      this.deviationReviewNotes,
    );
    this.message =
      result.kind === "synced"
        ? this.i18n.t(
            "mobile.operatorDeviationReviewSaved",
            "Deviation review saved.",
          )
        : operatorMobileErrorMessage(this.i18n, {
            status: result.kind === "conflict" ? result.statusCode : undefined,
            message: result.message,
          });
    if (result.kind === "synced") {
      this.api
        .getDeviations(undefined, "Open")
        .pipe(catchError(() => of([])))
        .subscribe((deviations) => {
          this.deviations = deviations;
          this.deviationId = "";
          this.cdr.markForCheck();
        });
    }
  }
}
