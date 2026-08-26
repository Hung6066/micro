import { Component, effect, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperatorMobileQrScannerService } from "../../core/services/operator-mobile-qr-scanner.service";
import { HisHopeTranslatePipe, HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { catchError, of } from "rxjs";
import { OperatorMobileApiService, type LotSummary } from "../../core/services/operator-mobile-api.service";
import { OperatorMobileTenantContextService } from "../../core/operator-mobile-tenant-context.service";

@Component({ standalone: true, imports: [FormsModule, HisHopeTranslatePipe], templateUrl: "./lot-scan-page.component.html", styleUrls: ["./lot-scan-page.component.scss"] })
export class LotScanPageComponent {
  private readonly scanner = inject(OperatorMobileQrScannerService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly api = inject(OperatorMobileApiService);
  private readonly tenant = inject(OperatorMobileTenantContextService);
  scannedCode = "";
  message = "";
  lots: LotSummary[] = [];

  constructor() {
    effect(() => {
      const tenantKey = this.tenant.activeTenantKey?.();
      if (!tenantKey) { this.lots = []; return; }
      this.api.getLots().pipe(catchError(() => of([]))).subscribe((lots) => {
        this.lots = lots;
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
    }
  }
}
