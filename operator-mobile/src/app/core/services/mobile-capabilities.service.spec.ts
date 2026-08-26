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

  it("maps dashboard visibility to admin.users.read", () => {
    permissions.has.and.callFake((code: string) => code === "admin.users.read");
    expect(capabilities.isFeatureEnabled("dashboard")).toBeTrue();
    expect(capabilities.isFeatureEnabled("clients")).toBeFalse();
  });

  it("maps write surfaces to mutation permissions", () => {
    permissions.has.and.callFake(
      (code: string) => code === "admin.roles.write",
    );
    expect(capabilities.isFeatureEnabled("manageRoles")).toBeTrue();
    expect(capabilities.isFeatureEnabled("manageUsers")).toBeFalse();
  });
});
