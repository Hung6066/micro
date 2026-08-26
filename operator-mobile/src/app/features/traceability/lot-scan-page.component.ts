import { Component, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperatorMobileQrScannerService } from "../../core/services/operator-mobile-qr-scanner.service";
import { HisHopeTranslatePipe, HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";

@Component({ standalone: true, imports: [FormsModule, HisHopeTranslatePipe], templateUrl: "./lot-scan-page.component.html", styleUrls: ["./lot-scan-page.component.scss"] })
export class LotScanPageComponent {
  private readonly scanner = inject(OperatorMobileQrScannerService);
  private readonly i18n = inject(HisHopeI18nService);
  scannedCode = "";
  message = "";

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
}
