import { TestBed } from "@angular/core/testing";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { MobileCapabilitiesService } from "./mobile-capabilities.service";

describe("MobileCapabilitiesService", () => {
  let permissions: jasmine.SpyObj<HisHopePermissionService>;
  let capabilities: MobileCapabilitiesService;

  beforeEach(() => {
    permissions = jasmine.createSpyObj("HisHopePermissionService", ["has"]);
    TestBed.configureTestingModule({
      providers: [
        MobileCapabilitiesService,
        { provide: HisHopePermissionService, useValue: permissions },
      ],
    });
    capabilities = TestBed.inject(MobileCapabilitiesService);
  });

  it("maps production visibility to manufacturing.production.execute", () => {
    permissions.has.and.callFake(
      (code: string) => code === "manufacturing.production.execute",
    );
    expect(capabilities.isFeatureEnabled("production")).toBeTrue();
    expect(capabilities.isFeatureEnabled("quality")).toBeFalse();
  });

  it("maps maintenance mutations to manufacturing.maintenance.complete", () => {
    permissions.has.and.callFake(
      (code: string) => code === "manufacturing.maintenance.complete",
    );
    expect(capabilities.canMutateMaintenance()).toBeTrue();
    expect(capabilities.canMutateQuality()).toBeFalse();
  });
});
