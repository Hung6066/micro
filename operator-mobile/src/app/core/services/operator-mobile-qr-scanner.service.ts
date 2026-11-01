import { Injectable } from "@angular/core";
import { Capacitor } from "@capacitor/core";
import { BarcodeFormat, BarcodeScanner } from "@capacitor-mlkit/barcode-scanning";

@Injectable({ providedIn: "root" })
export class OperatorMobileQrScannerService {
  async scanLot(): Promise<string | null> {
    if (!Capacitor.isNativePlatform()) return this.scanWithBarcodeDetector();
    const support = await BarcodeScanner.isSupported();
    if (!support.supported) return null;
    const permissions = await BarcodeScanner.checkPermissions();
    if (permissions.camera !== "granted") {
      const requested = await BarcodeScanner.requestPermissions();
      if (requested.camera !== "granted") return null;
    }
    const result = await BarcodeScanner.scan({ formats: [BarcodeFormat.QrCode] });
    return result.barcodes.find((barcode) => barcode.rawValue)?.rawValue ?? null;
  }

  private async scanWithBarcodeDetector(): Promise<string | null> {
    const detector = (globalThis as typeof globalThis & {
      BarcodeDetector?: new (options?: { formats?: string[] }) => {
        detect(source: ImageBitmapSource): Promise<Array<{ rawValue?: string }>>;
      };
    }).BarcodeDetector;
    if (!detector || !navigator.mediaDevices?.getUserMedia) return null;
    const video = document.createElement("video");
    video.setAttribute("playsinline", "true");
    video.muted = true;
    const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" } });
    video.srcObject = stream;
    await video.play();
    try {
      const reader = new detector({ formats: ["qr_code"] });
      const deadline = Date.now() + 10_000;
      while (Date.now() < deadline) {
        const codes = await reader.detect(video);
        const value = codes.find((code) => code.rawValue)?.rawValue;
        if (value) return value;
        await new Promise((resolve) => setTimeout(resolve, 120));
      }
      return null;
    } finally {
      stream.getTracks().forEach((track) => track.stop());
      video.srcObject = null;
    }
  }
}
