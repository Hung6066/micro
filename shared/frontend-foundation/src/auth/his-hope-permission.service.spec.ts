import { TestBed } from "@angular/core/testing";
import { HisHopePermissionService } from "./his-hope-permission.service";

describe("HisHopePermissionService", () => {
  let service: HisHopePermissionService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(HisHopePermissionService);
  });

  it("has no snapshot and denies specific permissions before one is set", () => {
    expect(service.hasSnapshot()).toBeFalse();
    expect(service.has("admin.users.write")).toBeFalse();
  });

  it("treats an empty permission string as always allowed", () => {
    expect(service.has("")).toBeTrue();
  });

  it("grants everything once a wildcard permission is present", () => {
    service.setPermissions(["*"]);
    expect(service.has("anything.at.all")).toBeTrue();
  });

  it("matches a scoped wildcard like admin.*", () => {
    service.setPermissions(["admin.*"]);
    expect(service.has("admin.users.write")).toBeTrue();
    expect(service.has("billing.invoices.read")).toBeFalse();
  });

  it("supports hasAny and hasAll across a permission set", () => {
    service.setPermissions(["patients.read", "patients.write"]);
    expect(service.hasAny(["billing.read", "patients.write"])).toBeTrue();
    expect(service.hasAll(["patients.read", "patients.write"])).toBeTrue();
    expect(service.hasAll(["patients.read", "billing.read"])).toBeFalse();
  });

  it("setSnapshot trims whitespace and drops empty entries", () => {
    service.setSnapshot({ permissions: [" patients.read ", "", "  "] });
    expect(service.has("patients.read")).toBeTrue();
  });

  it("clear() removes every granted permission", () => {
    service.setPermissions(["patients.read"]);
    service.clear();
    expect(service.has("patients.read")).toBeFalse();
    expect(service.hasSnapshot()).toBeFalse();
  });
});
