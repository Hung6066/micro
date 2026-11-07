import { OperatorMobileQrScannerService } from "./operator-mobile-qr-scanner.service";

describe("OperatorMobileQrScannerService", () => {
  let service: OperatorMobileQrScannerService;
  let originalDetector: unknown;
  let originalMediaDevices: MediaDevices | undefined;
  let originalSrcObjectDescriptor: PropertyDescriptor | undefined;
  let stopped = false;

  beforeEach(() => {
    service = new OperatorMobileQrScannerService();
    originalDetector = (globalThis as typeof globalThis & { BarcodeDetector?: unknown }).BarcodeDetector;
    originalMediaDevices = navigator.mediaDevices;
    originalSrcObjectDescriptor = Object.getOwnPropertyDescriptor(HTMLMediaElement.prototype, "srcObject");
    stopped = false;
  });

  afterEach(() => {
    (globalThis as typeof globalThis & { BarcodeDetector?: unknown }).BarcodeDetector = originalDetector;
    Object.defineProperty(navigator, "mediaDevices", { configurable: true, value: originalMediaDevices });
    if (originalSrcObjectDescriptor) {
      Object.defineProperty(HTMLMediaElement.prototype, "srcObject", originalSrcObjectDescriptor);
    }
  });

  it("uses the web camera fallback and releases the camera after a QR result", async () => {
    class FakeBarcodeDetector {
      async detect(): Promise<Array<{ rawValue?: string }>> {
        return [{ rawValue: "LOT-QR-001" }];
      }
    }
    const track = { stop: () => { stopped = true; } };
    const stream = { getTracks: () => [track] };
    (globalThis as typeof globalThis & { BarcodeDetector?: unknown }).BarcodeDetector = FakeBarcodeDetector;
    Object.defineProperty(navigator, "mediaDevices", {
      configurable: true,
      value: { getUserMedia: async () => stream },
    });
    Object.defineProperty(HTMLMediaElement.prototype, "srcObject", {
      configurable: true,
      get: () => null,
      set: () => undefined,
    });
    spyOn(HTMLMediaElement.prototype, "play").and.resolveTo();

    await expectAsync(service.scanLot()).toBeResolvedTo("LOT-QR-001");
    expect(stopped).toBeTrue();
  });

  it("returns null when the browser has no QR camera capability", async () => {
    (globalThis as typeof globalThis & { BarcodeDetector?: unknown }).BarcodeDetector = undefined;
    Object.defineProperty(navigator, "mediaDevices", { configurable: true, value: undefined });

    await expectAsync(service.scanLot()).toBeResolvedTo(null);
  });
});
