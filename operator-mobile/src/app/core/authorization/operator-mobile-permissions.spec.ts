import { operatorMobilePermissions } from "./operator-mobile-permissions";

describe("operatorMobilePermissions", () => {
  it("maps field operations to backend manufacturing permissions", () => {
    expect(operatorMobilePermissions.production.access).toBe(
      "manufacturing.production.execute",
    );
    expect(operatorMobilePermissions.quality.access).toBe(
      "manufacturing.quality.inspect",
    );
    expect(operatorMobilePermissions.maintenance.access).toBe(
      "manufacturing.maintenance.complete",
    );
    expect(operatorMobilePermissions.traceability.access).toBe(
      "manufacturing.production.execute",
    );
  });
});
