import { Component, inject } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { OperatorMobileQrScannerService } from "../../core/services/operator-mobile-qr-scanner.service";

@Component({ standalone: true, imports: [FormsModule], templateUrl: "./lot-scan-page.component.html", styleUrls: ["./lot-scan-page.component.scss"] })
export class LotScanPageComponent {
  private readonly scanner = inject(OperatorMobileQrScannerService);
  scannedCode = "";
  message = "";

  async scanQr(): Promise<void> {
    this.message = "";
    try {
      const value = await this.scanner.scanLot();
      this.scannedCode = value ?? this.scannedCode;
      this.message = value ? "Lot code captured." : "QR scanner unavailable or no code was captured.";
    } catch {
      this.message = "Camera permission is required to scan a lot.";
    }
  }
}
