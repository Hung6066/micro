import { TestBed } from "@angular/core/testing";
import { Capacitor } from "@capacitor/core";
import { of } from "rxjs";
import { MobileAuthService } from "./auth.service";
import { NativeCapabilityService } from "./native-capability.service";
import { MobilePlatformService } from "./mobile-platform.service";
import { MobileDeviceAttestationService } from "./mobile-device-attestation.service";

describe("MobileDeviceAttestationService", () => {
  let service: MobileDeviceAttestationService;
  let submitSpy: jasmine.Spy;

  const nativeAttestation = {
    provider: "play-integrity" as const,
    signals: {
      device_secure: true,
      not_rooted: true,
      not_emulator: true,
      not_debuggable: true,
      play_integrity_available: false,
      play_integrity_verdict: false,
    },
  };

  beforeEach(() => {
    submitSpy = jasmine.createSpy("submitDeviceAttestation").and.resolveTo(undefined);
    TestBed.configureTestingModule({
      providers: [
        MobileDeviceAttestationService,
        {
          provide: MobileAuthService,
          useValue: {
            isAuthenticated$: of(true),
            userData$: of({ userData: { sub: "11111111-1111-1111-1111-111111111111" }, allUserData: [] }),
          },
        },
        {
          provide: NativeCapabilityService,
          useValue: {
            secureGet: jasmine.createSpy("secureGet").and.resolveTo(null),
            secureSet: jasmine.createSpy("secureSet").and.resolveTo(undefined),
          },
        },
        {
          provide: MobilePlatformService,
          useValue: {
            deviceSecurity: jasmine.createSpy("deviceSecurity").and.resolveTo({
              status: "secure",
              rootedOrJailbroken: false,
              emulator: false,
              debuggable: false,
            }),
            deviceAttestation: jasmine.createSpy("deviceAttestation").and.resolveTo(nativeAttestation),
            submitDeviceAttestation: submitSpy,
          },
        },
      ],
    });
    service = TestBed.inject(MobileDeviceAttestationService);
  });

  it("forwards native attestation signals to the backend", () => {
    const payload = service.buildSubmission(
      nativeAttestation,
      "11111111-1111-1111-1111-111111111111",
      "device-1",
      "nonce-1",
    );

    expect(payload.provider).toBe("play-integrity");
    expect(payload.signals["play_integrity_available"]).toBeFalse();
  });

  it("submits attestation when authenticated on native platforms", async () => {
    spyOn(Capacitor, "isNativePlatform").and.returnValue(true);

    await service.submitIfEligible({
      status: "secure",
      rootedOrJailbroken: false,
      emulator: false,
      debuggable: false,
    });

    expect(submitSpy).toHaveBeenCalled();
  });

  it("skips attestation on web", async () => {
    spyOn(Capacitor, "isNativePlatform").and.returnValue(false);

    await service.submitIfEligible({
      status: "secure",
      rootedOrJailbroken: false,
      emulator: false,
      debuggable: false,
    });

    expect(submitSpy).not.toHaveBeenCalled();
  });
});
