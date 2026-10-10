import { ComponentFixture, TestBed } from "@angular/core/testing";
import { HisHopeThemeService } from "@his-hope/frontend-foundation";
import { AppComponent } from "./app.component";
import { MobileAuthService } from "./core/auth.service";
import { MobilePlatformService } from "./core/mobile-platform.service";
import { NativeCapabilityService } from "./core/native-capability.service";
import { MobileTelemetryService } from "./core/mobile-telemetry.service";
import { MobileDeviceAttestationService } from "./core/mobile-device-attestation.service";
import { MobilePlatformCapabilitiesService } from "./core/services/mobile-platform-capabilities.service";
import { of } from "rxjs";
import { signal } from "@angular/core";

describe("AppComponent", () => {
  let fixture: ComponentFixture<AppComponent>;
  let theme: jasmine.SpyObj<HisHopeThemeService>;

  beforeEach(async () => {
    theme = jasmine.createSpyObj<HisHopeThemeService>("HisHopeThemeService", [
      "restore",
      "setPlatform",
    ]);
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        { provide: HisHopeThemeService, useValue: theme },
        { provide: MobileAuthService, useValue: { checkAuth: () => of(false), isAuthenticated$: of(false), userData$: of(null), logout: jasmine.createSpy("logout") } },
        { provide: MobilePlatformService, useValue: { maintenance: signal(false), upgradeRequired: signal(false), storeUrl: signal(null), appPolicy: () => Promise.resolve({ maintenance: false, forceUpgrade: false, minimumVersion: "", storeUrl: null }), deviceSecurity: () => Promise.resolve({ status: "unsupported", rootedOrJailbroken: false, emulator: false, debuggable: true }), configureCertificatePins: () => Promise.resolve() } },
        { provide: NativeCapabilityService, useValue: { initialize: () => Promise.resolve() } },
        { provide: MobileTelemetryService, useValue: { initialize: jasmine.createSpy("initialize") } },
        { provide: MobileDeviceAttestationService, useValue: { submitIfEligible: () => Promise.resolve() } },
        { provide: MobilePlatformCapabilitiesService, useValue: { flushOfflineSync: () => Promise.resolve() } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
  });

  it("restores shared theme preferences and selects mobile density", () => {
    expect(theme.restore).toHaveBeenCalled();
    expect(theme.setPlatform).toHaveBeenCalledWith("mobile");
  });
});
