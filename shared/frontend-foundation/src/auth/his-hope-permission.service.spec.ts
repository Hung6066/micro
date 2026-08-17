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

  it("fails closed when the authorization snapshot is expired", () => {
    service.setSnapshot({
      permissions: ["patients.read"],
      expiresAt: new Date(Date.now() - 1000).toISOString(),
    });
    expect(service.hasSnapshot()).toBeFalse();
    expect(service.has("patients.read")).toBeFalse();
  });

  it("normalizes facility membership metadata with the snapshot", () => {
    service.setSnapshot({
      permissions: ["patients.read"],
      facilityIds: [" facility-a ", "facility-a", "facility-b"],
    });
    expect(service.snapshot()?.facilityIds).toEqual(["facility-a", "facility-b"]);
    expect(service.has("patients.read")).toBeTrue();
  });

  it("exposes OAuth scopes as normalized UX entitlements", () => {
    service.setSnapshot({ permissions: ["patients.view"], scopes: [" fhir.patient.read ", "fhir.patient.read"] });
    expect(service.hasScope("fhir.patient.read")).toBeTrue();
    expect(service.hasScope("fhir.encounter.read")).toBeFalse();
    expect(service.hasAllScopes(["fhir.patient.read"])).toBeTrue();
  });

  it("clears the snapshot on an authentication failure and records the denial", () => {
    service.setPermissions(["patients.view"]);
    service.recordAuthorizationFailure(401, "patients.view");

    expect(service.hasSnapshot()).toBeFalse();
    expect(service.lastAuthorizationFailure()?.status).toBe(401);
    expect(service.lastAuthorizationFailure()?.action).toBe("patients.view");
  });

  it("keeps the snapshot on a resource denial but exposes failure state to UX", () => {
    service.setPermissions(["patients.view"]);
    service.recordAuthorizationFailure(403, "patients.view");

    expect(service.has("patients.view")).toBeTrue();
    expect(service.lastAuthorizationFailure()?.status).toBe(403);
  });
});
