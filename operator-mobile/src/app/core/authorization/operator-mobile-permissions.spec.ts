import { operatorMobilePermissions } from "./operator-mobile-permissions";

describe("operatorMobilePermissions", () => {
  it("maps field mutations to narrow permissions", () => {
    expect(operatorMobilePermissions.production.execute).toBe("manufacturing.production.execute");
    expect(operatorMobilePermissions.quality.inspect).toBe("manufacturing.quality.inspect");
    expect(operatorMobilePermissions.maintenance.complete).toBe("manufacturing.maintenance.complete");
  });
});
