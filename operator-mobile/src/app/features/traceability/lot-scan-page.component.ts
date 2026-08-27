import { ChangeDetectorRef, Component, effect, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperatorMobileQrScannerService } from "../../core/services/operator-mobile-qr-scanner.service";
import { HisHopeTranslatePipe, HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { catchError, of } from "rxjs";
import { OperatorMobileApiService, type FefoLot, type InventoryTransaction, type LotGenealogy, type LotStatusHistory, type LotSummary, type ManufacturingAvailability, type QualityInspection } from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";
import { manufacturingEnumLabel } from "../../core/manufacturing-enum-label.util";

@Component({ standalone: true, imports: [FormsModule, HisHopeTranslatePipe], templateUrl: "./lot-scan-page.component.html", styleUrls: ["./lot-scan-page.component.scss"] })
export class LotScanPageComponent {
  private readonly scanner = inject(OperatorMobileQrScannerService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly api = inject(OperatorMobileApiService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  private readonly cdr = inject(ChangeDetectorRef);
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

  dispositionLabel(disposition: string): string { return manufacturingEnumLabel(this.i18n, "disposition", disposition); }
  genealogyRoleLabel(role: string): string { return manufacturingEnumLabel(this.i18n, "genealogyRole", role); }
  formatNumber(value: number): string { return this.i18n.formatNumber(value); }
  inspectionStatusLabel(status: string): string { return manufacturingEnumLabel(this.i18n, "qualityInspectionStatus", status); }
  inspectionDateLabel(value: string): string { return this.i18n.formatDateTime(value); }
  transactionTypeLabel(type: string): string { return manufacturingEnumLabel(this.i18n, "inventoryTransactionTypes", type); }
  stockStatusLabel(status: string): string { return manufacturingEnumLabel(this.i18n, "stockStatus", status); }
  lotOptionLabel(lot: LotSummary): string {
    const expiry = lot.bestBefore ? ` · ${this.i18n.t("mobile.operatorBestBefore", "Best before")}: ${this.i18n.formatDate(lot.bestBefore)}` : "";
    return `${lot.lotCode || lot.sku} · ${this.dispositionLabel(lot.disposition)}${expiry}`;
  }

  constructor() {
    effect(() => {
      const tenantKey = this.tenant.activeTenantKey?.();
      if (!tenantKey) { this.lots = []; return; }
      this.api.getLots().pipe(catchError(() => { this.loadError = this.i18n.t("mobile.operatorDataLoadFailed", "Unable to load operational data. Check your connection and permissions."); this.cdr.markForCheck(); return of([]); })).subscribe((lots) => {
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
      this.message = value ? this.i18n.t("mobile.operatorLotCaptured", "Lot code captured.") : this.i18n.t("mobile.operatorScannerUnavailable", "QR scanner unavailable or no code was captured.");
    } catch {
      this.message = this.i18n.t("mobile.operatorCameraPermission", "Camera permission is required to scan a lot.");
    }
  }

  openLot(): void {
    if (!this.scannedCode) {
      this.message = this.i18n.t("mobile.operatorChooseLotFirst", "Choose a lot first.");
      return;
    }
    const selected = this.lots.find((lot) => (lot.lotCode || lot.id) === this.scannedCode);
    if (!selected) { this.message = this.i18n.t("mobile.operatorLotNotFound", "The selected lot could not be found."); return; }
    this.genealogy = null;
    this.qualityHistory = [];
    this.statusHistory = [];
    this.inventoryHistory = [];
    this.availability = null;
    this.fefoLots = [];
    this.message = "";
    this.cdr.markForCheck();
    this.api.getLotQualityInspections(selected.id).pipe(catchError(() => of([]))).subscribe((history) => {
      setTimeout(() => { this.qualityHistory = history; this.cdr.markForCheck(); });
    });
    this.api.getLotStatusHistory(selected.id).pipe(catchError(() => of([]))).subscribe((history) => {
      setTimeout(() => { this.statusHistory = history; this.cdr.markForCheck(); });
    });
    this.api.getLotInventoryTransactions(selected.id).pipe(catchError(() => of([]))).subscribe((history) => {
      setTimeout(() => { this.inventoryHistory = history; this.cdr.markForCheck(); });
    });
    this.api.getLotGenealogy(selected.id).pipe(catchError(() => of(null))).subscribe((genealogy) => {
      setTimeout(() => {
        // Preserve raw contract values and localize at render time.
        this.genealogy = genealogy;
        this.message = genealogy ? this.i18n.t("mobile.operatorGenealogyLoaded", "Genealogy loaded.") : this.i18n.t("mobile.operatorGenealogyFailed", "Unable to load genealogy.");
        this.cdr.markForCheck();
      });
    });
    this.api.getAvailability(selected.sku).pipe(catchError(() => of(null))).subscribe((availability) => {
      setTimeout(() => { this.availability = availability; this.cdr.markForCheck(); });
    });
    this.api.getFefoLots(selected.sku).pipe(catchError(() => of([]))).subscribe((fefoLots) => {
      setTimeout(() => { this.fefoLots = fefoLots; this.cdr.markForCheck(); });
    });
  }
}
