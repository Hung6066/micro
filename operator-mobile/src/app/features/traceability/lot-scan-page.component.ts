import { ChangeDetectorRef, Component, effect, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperatorMobileQrScannerService } from "../../core/services/operator-mobile-qr-scanner.service";
import {
  HisHopeTranslatePipe,
  HisHopeI18nService,
} from "@his-hope/frontend-foundation/i18n";
import { catchError, of } from "rxjs";
import {
  OperatorMobileApiService,
  type FefoLot,
  type InventoryTransaction,
  type LotGenealogy,
  type LotStatusHistory,
  type LotSummary,
  type ManufacturingAvailability,
  type QualityInspection,
  type RecallImpact,
} from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import { manufacturingEnumLabel } from "../../core/manufacturing-enum-label.util";
import {
  HisHopeSelectComponent,
  HisHopeTabsComponent,
} from "@his-hope/frontend-foundation/ui";
import { OperationQueueService } from "../../core/offline/operation-queue.service";
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
  templateUrl: "./lot-scan-page.component.html",
  styleUrls: ["./lot-scan-page.component.scss"],
})
export class LotScanPageComponent {
  activeTab: "scan" | "history" = "scan";
  private readonly scanner = inject(OperatorMobileQrScannerService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly api = inject(OperatorMobileApiService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly queue = inject(OperationQueueService);
  private readonly native = inject(NativeCapabilityService);
  scannedCode = "";
  message = "";
  loadError = "";
  lots: LotSummary[] = [];
  genealogy: LotGenealogy | null = null;
  qualityHistory: QualityInspection[] = [];
  statusHistory: LotStatusHistory[] = [];
  inventoryHistory: InventoryTransaction[] = [];
  availability: ManufacturingAvailability | null = null;
  fefoLots: FefoLot[] = [];
  recallImpact: RecallImpact | null = null;
  selectedLot: LotSummary | null = null;
  newDisposition = "";
  dispositionReason = "";
  dispositionEvidence = "";

  async captureEvidence(): Promise<void> {
    const photo = await this.native.capturePhoto({
      quality: 82,
      width: 1600,
      height: 1600,
    });
    if (photo?.uri) this.dispositionEvidence = photo.uri;
  }

  dispositionLabel(disposition: string): string {
    return manufacturingEnumLabel(this.i18n, "disposition", disposition);
  }
  genealogyRoleLabel(role: string): string {
    return manufacturingEnumLabel(this.i18n, "genealogyRole", role);
  }
  formatNumber(value: number): string {
    return this.i18n.formatNumber(value);
  }
  inspectionStatusLabel(status: string): string {
    return manufacturingEnumLabel(this.i18n, "qualityInspectionStatus", status);
  }
  inspectionDateLabel(value: string): string {
    return this.i18n.formatDateTime(value);
  }
  transactionTypeLabel(type: string): string {
    return manufacturingEnumLabel(this.i18n, "inventoryTransactionTypes", type);
  }
  stockStatusLabel(status: string): string {
    return manufacturingEnumLabel(this.i18n, "stockStatus", status);
  }
  recallRelationLabel(relation: string): string {
    return manufacturingEnumLabel(this.i18n, "recallRelation", relation);
  }
  lotOptionLabel(lot: LotSummary): string {
    const expiry = lot.bestBefore
      ? ` · ${this.i18n.t("mobile.operatorBestBefore", "Best before")}: ${this.i18n.formatDate(lot.bestBefore)}`
      : "";
    return `${lot.lotCode || lot.sku} · ${this.dispositionLabel(lot.disposition)}${expiry}`;
  }

  constructor() {
    effect(() => {
      const tenantKey = this.tenant.activeTenantKey?.();
      if (!tenantKey) {
        this.lots = [];
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
            // Keep the backend enum value intact; the template resolves it through
            // the active dictionary so switching language updates existing options.
            this.lots = lots;
            this.cdr.markForCheck();
          });
        });
    });
  }

  async scanQr(): Promise<void> {
    this.message = "";
    try {
      const value = await this.scanner.scanLot();
      this.scannedCode = value ?? this.scannedCode;
      this.message = value
        ? this.i18n.t("mobile.operatorLotCaptured", "Lot code captured.")
        : this.i18n.t(
            "mobile.operatorScannerUnavailable",
            "QR scanner unavailable or no code was captured.",
          );
    } catch {
      this.message = this.i18n.t(
        "mobile.operatorCameraPermission",
        "Camera permission is required to scan a lot.",
      );
    }
  }

  openLot(): void {
    if (!this.scannedCode) {
      this.message = this.i18n.t(
        "mobile.operatorChooseLotFirst",
        "Choose a lot first.",
      );
      return;
    }
    const selected = this.lots.find(
      (lot) => (lot.lotCode || lot.id) === this.scannedCode,
    );
    if (!selected) {
      this.message = this.i18n.t(
        "mobile.operatorLotNotFound",
        "The selected lot could not be found.",
      );
      return;
    }
    this.genealogy = null;
    this.selectedLot = selected;
    this.newDisposition = selected.disposition;
    this.qualityHistory = [];
    this.statusHistory = [];
    this.inventoryHistory = [];
    this.availability = null;
    this.fefoLots = [];
    this.recallImpact = null;
    this.message = "";
    this.cdr.markForCheck();
    this.api
      .getLotQualityInspections(selected.id)
      .pipe(catchError(() => of([])))
      .subscribe((history) => {
        setTimeout(() => {
          this.qualityHistory = history;
          this.cdr.markForCheck();
        });
      });
    this.api
      .getLotStatusHistory(selected.id)
      .pipe(catchError(() => of([])))
      .subscribe((history) => {
        setTimeout(() => {
          this.statusHistory = history;
          this.cdr.markForCheck();
        });
      });
    this.api
      .getLotInventoryTransactions(selected.id)
      .pipe(catchError(() => of([])))
      .subscribe((history) => {
        setTimeout(() => {
          this.inventoryHistory = history;
          this.cdr.markForCheck();
        });
      });
    this.api
      .getLotGenealogy(selected.id)
      .pipe(catchError(() => of(null)))
      .subscribe((genealogy) => {
        setTimeout(() => {
          // Preserve raw contract values and localize at render time.
          this.genealogy = genealogy;
          this.message = genealogy
            ? this.i18n.t("mobile.operatorGenealogyLoaded", "Genealogy loaded.")
            : this.i18n.t(
                "mobile.operatorGenealogyFailed",
                "Unable to load genealogy.",
              );
          this.cdr.markForCheck();
        });
      });
    this.api
      .getAvailability(selected.sku)
      .pipe(catchError(() => of(null)))
      .subscribe((availability) => {
        setTimeout(() => {
          this.availability = availability;
          this.cdr.markForCheck();
        });
      });
    this.api
      .getFefoLots(selected.sku)
      .pipe(catchError(() => of([])))
      .subscribe((fefoLots) => {
        setTimeout(() => {
          this.fefoLots = fefoLots;
          this.cdr.markForCheck();
        });
      });
    this.api
      .getRecallImpact(selected.id)
      .pipe(catchError(() => of(null)))
      .subscribe((recallImpact) => {
        setTimeout(() => {
          this.recallImpact = recallImpact;
          this.cdr.markForCheck();
        });
      });
  }

  async changeDisposition(): Promise<void> {
    const scope = this.tenant.commandScope;
    if (
      !scope ||
      !this.selectedLot ||
      !this.newDisposition ||
      this.newDisposition === this.selectedLot.disposition
    ) {
      this.message = this.i18n.t(
        "mobile.operatorLotDispositionValidation",
        "Choose a new lot disposition.",
      );
      return;
    }
    const operation = await this.queue.submit(
      {
        ...scope,
        endpoint: `/lots/${this.selectedLot.id}/disposition`,
        payload: {
          disposition: this.newDisposition,
          actor: scope.subjectId,
          reasonCode: this.dispositionReason.trim() || undefined,
          evidenceReference: this.dispositionEvidence.trim() || undefined,
          expectedUpdatedAt: this.selectedLot.updatedAt,
        },
      },
      (queued) => this.api.changeLotDisposition(queued),
    );
    this.message =
      operation.status === "synced"
        ? this.i18n.t(
            "mobile.operatorLotDispositionSaved",
            "Lot disposition saved.",
          )
        : this.i18n.t(
            "mobile.operatorPendingSync",
            "Pending sync — it will retry when connected.",
          );
    if (operation.status === "synced") {
      this.selectedLot = {
        ...this.selectedLot,
        disposition: this.newDisposition,
      };
      this.cdr.markForCheck();
    }
  }
}
