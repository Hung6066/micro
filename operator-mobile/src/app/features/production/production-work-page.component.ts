import { ChangeDetectorRef, Component, effect, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { catchError, of } from "rxjs";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
import {
  OperatorMobileApiService,
  type ManufacturingException,
  type ManufacturingOee,
  type ManufacturingProductionCost,
  type ManufacturingSummary,
  type ProductionBatch,
  type ProductionKpi,
  type ProductionOrder,
  type Recipe,
  type SopArtifact,
  type Machine,
  type LotSummary,
} from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { manufacturingEnumLabel } from "../../core/manufacturing-enum-label.util";
import { buildManufacturingWorkflowRenderModel } from "@his-hope/frontend-foundation/domain";
import { HisHopeWorkflowStepperComponent } from "@his-hope/frontend-foundation/ui";
import {
  HisHopeSelectComponent,
  HisHopeTabsComponent,
} from "@his-hope/frontend-foundation/ui";
import { operatorMobileErrorMessage } from "../../core/operator-mobile-error.util";

@Component({
  standalone: true,
  imports: [
    FormsModule,
    HisHopeTranslatePipe,
    HisHopeWorkflowStepperComponent,
    HisHopeSelectComponent,
    HisHopeTabsComponent,
  ],
  templateUrl: "./production-work-page.component.html",
  styleUrls: ["./production-work-page.component.scss"],
})
export class ProductionWorkPageComponent {
  activeTab: "work" | "overview" = "work";
  private readonly api = inject(OperatorMobileApiService);
  private readonly queue = inject(OperationQueueService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  batches: ProductionBatch[] = [];
  selectedBatchId = "";
  measurementOperationExecutionId = "";
  measurementMachineId = "";
  measurementLotId = "";
  machines: Machine[] = [];
  lots: LotSummary[] = [];
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
  orders: ProductionOrder[] = [];
  recipes: Recipe[] = [];
  sopArtifacts: SopArtifact[] = [];
  acknowledgedSopArtifactIds = new Set<string>();
  sopAcknowledgmentNotes = "";
  measurementType = "temperature";
  measurementValue = 0;
  measurementUom = "°C";
  lossOperationId = "";
  lossDecision = "Approved";
  lossReviewer = "";
  lossNotes = "";

  batchStatusLabel(status: string): string {
    return manufacturingEnumLabel(this.i18n, "productionBatchStatus", status);
  }
  dataCompletenessLabel(value: string | null | undefined): string {
    return manufacturingEnumLabel(this.i18n, "dataCompleteness", value);
  }
  formatNumber(value: number): string {
    return this.i18n.formatNumber(value);
  }
  formatPercent(value: number): string {
    return this.i18n.formatNumber(value, { maximumFractionDigits: 2 }) + "%";
  }
  formatCurrency(value: number): string {
    return this.i18n.formatCurrency(value);
  }
  qcStatusLabel(status: string): string {
    return manufacturingEnumLabel(this.i18n, "qualityInspectionStatus", status);
  }
  oeeStatusLabel(status: string): string {
    return manufacturingEnumLabel(this.i18n, "oeeStatus", status);
  }
  oeeMetricLabel(metric: string): string {
    return manufacturingEnumLabel(this.i18n, "oeeMissingMetrics", metric);
  }
  oeeMissingMetricsLabel(metrics: string[]): string {
    return metrics.map((metric) => this.oeeMetricLabel(metric)).join(", ");
  }
  exceptionSeverityLabel(severity: string): string {
    return manufacturingEnumLabel(this.i18n, "exceptionSeverity", severity);
  }
  exceptionCodeLabel(code: string): string {
    return manufacturingEnumLabel(this.i18n, "exceptionCode", code);
  }
  batchWorkflowSteps() {
    const batch = this.batches.find((item) => item.id === this.selectedBatchId);
    return batch
      ? buildManufacturingWorkflowRenderModel(
          "production-batch",
          batch.status,
          (group, key) => manufacturingEnumLabel(this.i18n, group, key),
        )
      : [];
  }

  selectedBatch(): ProductionBatch | undefined {
    return this.batches.find((batch) => batch.id === this.selectedBatchId);
  }

  selectedRecipe(): Recipe | undefined {
    const order = this.orders.find(
      (item) => item.id === this.selectedBatch()?.productionOrderId,
    );
    return this.recipes.find((recipe) => recipe.id === order?.recipeId);
  }

  selectedSopArtifact(): SopArtifact | undefined {
    const recipe = this.selectedRecipe();
    if (!recipe) return undefined;
    const product = recipe.productSku.toLowerCase();
    const step = recipe.processStep.toLowerCase();
    return (
      this.sopArtifacts.find(
        (artifact) =>
          artifact.artifactKey.toLowerCase().includes(product) ||
          artifact.artifactKey.toLowerCase().includes(step),
      ) ?? this.sopArtifacts[0]
    );
  }

  async acknowledgeSopArtifact(): Promise<void> {
    const artifact = this.selectedSopArtifact();
    if (!artifact) return;
    const result = await this.api.acknowledgeSopArtifact(
      artifact.id,
      this.sopAcknowledgmentNotes,
    );
    if (
      result.status === "synced" ||
      result.status === "already-acknowledged"
    ) {
      this.acknowledgedSopArtifactIds.add(artifact.id);
      this.message = this.i18n.t(
        "mobile.operatorSopAcknowledged",
        "SOP acknowledgement recorded.",
      );
    } else {
      this.message = this.i18n.t(
        "mobile.operatorSopAcknowledgmentOnline",
        "SOP acknowledgement requires an online authenticated session.",
      );
    }
    this.cdr.markForCheck();
  }

  lossOperations() {
    return (this.selectedBatch()?.operations ?? []).filter(
      (operation) => operation.lossQuantity > 0,
    );
  }

  async reviewLoss(): Promise<void> {
    const batch = this.selectedBatch();
    const scope = this.tenant.commandScope;
    if (!batch || !this.lossOperationId || !this.lossReviewer.trim()) {
      this.message = this.i18n.t(
        "mobile.operatorLossReviewValidation",
        "Select a loss operation and enter the reviewer.",
      );
      return;
    }
    if (!scope) {
      this.message = this.i18n.t(
        "mobile.operatorTenantRequired",
        "Sign in and select an operational tenant before continuing.",
      );
      return;
    }
    const operation = await this.queue.submit(
      {
        ...scope,
        endpoint: `/production-batches/${batch.id}/operations/${this.lossOperationId}/loss-review`,
        expectedVersion: batch.version,
        payload: {
          decision: this.lossDecision,
          reviewer: this.lossReviewer.trim(),
          notes: this.lossNotes.trim() || undefined,
        },
      },
      (queued) => this.api.reviewLoss(queued),
    );
    this.message =
      operation.status === "synced"
        ? this.i18n.t("mobile.operatorLossReviewed", "Loss review recorded.")
        : this.i18n.t(
            "mobile.operatorPendingSync",
            "Pending sync — it will retry when connected.",
          );
  }

  async transitionBatch(
    action: "pause" | "resume" | "complete" | "cancel",
  ): Promise<void> {
    const batch = this.selectedBatch();
    const scope = this.tenant.commandScope;
    if (!batch || !scope) {
      this.message = this.i18n.t(
        "mobile.operatorTenantRequired",
        "Sign in and select an operational tenant before continuing.",
      );
      return;
    }
    const operation = await this.queue.submit(
      {
        ...scope,
        endpoint: `/production-batches/${batch.id}/${action}`,
        expectedVersion: batch.version,
        payload: {},
      },
      (queued) => this.api.transitionProductionBatch(queued),
    );
    this.message =
      operation.status === "synced"
        ? this.i18n.t(
            "mobile.operatorBatchTransitioned",
            "Batch status updated.",
          )
        : this.i18n.t(
            "mobile.operatorPendingSync",
            "Pending sync — it will retry when connected.",
          );
    if (operation.status === "synced") {
      this.batches = this.batches.filter((item) => item.id !== batch.id);
      this.selectedBatchId = "";
      this.cdr.markForCheck();
    }
  }

  async recordMeasurement(): Promise<void> {
    const batch = this.selectedBatch();
    const scope = this.tenant.commandScope;
    if (
      !batch ||
      !scope ||
      !this.measurementType.trim() ||
      !this.measurementUom.trim()
    ) {
      this.message = this.i18n.t(
        "mobile.operatorMeasurementValidation",
        "Select a batch and enter measurement details.",
      );
      return;
    }
    const operation = await this.queue.submit(
      {
        ...scope,
        endpoint: `/production-batches/${batch.id}/measurements`,
        expectedVersion: batch.version,
        payload: {
          productionBatchId: batch.id,
          operationExecutionId: this.measurementOperationExecutionId || null,
          machineId: this.measurementMachineId || null,
          lotId: this.measurementLotId || null,
          measurementType: this.measurementType.trim(),
          value: this.measurementValue,
          uom: this.measurementUom.trim(),
          measuredAt: new Date().toISOString(),
          source: "operator-mobile",
        },
      },
      (queued) => this.api.recordOperationMeasurement(queued),
    );
    this.message =
      operation.status === "synced"
        ? this.i18n.t(
            "mobile.operatorMeasurementRecorded",
            "Measurement recorded.",
          )
        : this.i18n.t(
            "mobile.operatorPendingSync",
            "Pending sync — it will retry when connected.",
          );
  }

  constructor() {
    effect(() => {
      if (!this.tenant.activeTenantKey()) {
        this.batches = [];
        this.summary = null;
        this.kpis = null;
        this.oee = null;
        this.exceptions = [];
        this.costs = null;
        this.orders = [];
        this.recipes = [];
        this.sopArtifacts = [];
        this.acknowledgedSopArtifactIds.clear();
        this.selectedBatchId = "";
        this.measurementOperationExecutionId = "";
        this.measurementMachineId = "";
        this.measurementLotId = "";
        this.machines = [];
        this.lots = [];
        return;
      }
      this.api
        .getProductionBatches("Started")
        .pipe(
          catchError((error) => {
            this.loadError = operatorMobileErrorMessage(this.i18n, error);
            this.cdr.markForCheck();
            return of([]);
          }),
        )
        .subscribe((batches) => {
          setTimeout(() => {
            this.batches = batches;
            if (
              this.selectedBatchId &&
              !batches.some((batch) => batch.id === this.selectedBatchId)
            )
              this.selectedBatchId = "";
            this.cdr.markForCheck();
          });
        });
      this.api
        .getProductionOrders("Released")
        .pipe(catchError(() => of([])))
        .subscribe((orders) => {
          setTimeout(() => {
            this.orders = orders;
            this.cdr.markForCheck();
          });
        });
      this.api
        .getMachines("Available")
        .pipe(catchError(() => of([])))
        .subscribe((machines) => {
          setTimeout(() => {
            this.machines = machines;
            if (
              !machines.some(
                (machine) => machine.id === this.measurementMachineId,
              )
            )
              this.measurementMachineId = "";
            this.cdr.markForCheck();
          });
        });
      this.api
        .getLots()
        .pipe(catchError(() => of([])))
        .subscribe((lots) => {
          setTimeout(() => {
            this.lots = lots;
            if (!lots.some((lot) => lot.id === this.measurementLotId))
              this.measurementLotId = "";
            this.cdr.markForCheck();
          });
        });
      this.api
        .getRecipes(undefined, "Approved")
        .pipe(catchError(() => of([])))
        .subscribe((recipes) => {
          setTimeout(() => {
            this.recipes = recipes;
            this.cdr.markForCheck();
          });
        });
      this.api
        .getSopArtifacts(undefined, "Approved")
        .pipe(catchError(() => of([])))
        .subscribe((artifacts) => {
          setTimeout(() => {
            this.sopArtifacts = artifacts.filter(
              (artifact) => artifact.status === "Approved",
            );
            this.cdr.markForCheck();
          });
        });
      this.api
        .getManufacturingSummary()
        .pipe(
          catchError((error) => {
            this.loadError = operatorMobileErrorMessage(this.i18n, error);
            this.cdr.markForCheck();
            return of(null);
          }),
        )
        .subscribe((summary) => {
          setTimeout(() => {
            this.summary = summary;
            this.cdr.markForCheck();
          });
        });
      this.api
        .getProductionKpis()
        .pipe(
          catchError((error) => {
            this.loadError = operatorMobileErrorMessage(this.i18n, error);
            this.cdr.markForCheck();
            return of(null);
          }),
        )
        .subscribe((kpis) => {
          setTimeout(() => {
            this.kpis = kpis;
            this.cdr.markForCheck();
          });
        });
      this.api
        .getOee()
        .pipe(
          catchError((error) => {
            this.loadError = operatorMobileErrorMessage(this.i18n, error);
            this.cdr.markForCheck();
            return of(null);
          }),
        )
        .subscribe((oee) => {
          setTimeout(() => {
            this.oee = oee;
            this.cdr.markForCheck();
          });
        });
      this.api
        .getExceptions()
        .pipe(
          catchError((error) => {
            this.loadError = operatorMobileErrorMessage(this.i18n, error);
            this.cdr.markForCheck();
            return of([]);
          }),
        )
        .subscribe((exceptions) => {
          setTimeout(() => {
            this.exceptions = exceptions.slice(0, 5);
            this.cdr.markForCheck();
          });
        });
      this.api
        .getProductionCosts()
        .pipe(
          catchError((error) => {
            this.loadError = operatorMobileErrorMessage(this.i18n, error);
            this.cdr.markForCheck();
            return of(null);
          }),
        )
        .subscribe((costs) => {
          setTimeout(() => {
            this.costs = costs;
            this.cdr.markForCheck();
          });
        });
    });
  }

  async recordOperation(): Promise<void> {
    const selectedBatch = this.batches.find(
      (batch) => batch.id === this.selectedBatchId,
    );
    if (!selectedBatch || this.inputQuantity <= 0 || this.outputQuantity <= 0) {
      this.message = this.i18n.t(
        "mobile.operatorProductionValidation",
        "Select a batch and enter a positive output quantity.",
      );
      return;
    }
    const scope = this.tenant.commandScope;
    if (!scope) {
      this.message = this.i18n.t(
        "mobile.operatorTenantRequired",
        "Sign in and select an operational tenant before recording work.",
      );
      return;
    }
    const operation = await this.queue.submit(
      {
        ...scope,
        endpoint: `/production-batches/${selectedBatch.id}/operations`,
        expectedVersion: selectedBatch.version,
        payload: {
          sequence: 1,
          processStep: this.processStep.trim() || "operation",
          operator: scope.subjectId,
          inputQuantity: this.inputQuantity,
          outputQuantity: this.outputQuantity,
          required: true,
          qcStatus: this.qcStatus,
        },
      },
      (queued) => this.api.recordProductionOperation(queued),
    );
    this.message =
      operation.status === "synced"
        ? this.i18n.t("mobile.operatorOperationRecorded", "Operation recorded.")
        : this.i18n.t(
            "mobile.operatorPendingSync",
            "Pending sync — it will retry when connected.",
          );
  }
}
