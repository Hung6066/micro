import type { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { manufacturingEnumLabel } from "./manufacturing-enum-label.util";

describe("manufacturingEnumLabel", () => {
  it("resolves enum values through the shared dictionary", () => {
    const i18n = { t: jasmine.createSpy("t").and.returnValue("Đã bắt đầu") } as unknown as HisHopeI18nService;
    expect(manufacturingEnumLabel(i18n, "productionBatchStatus", "Started")).toBe("Đã bắt đầu");
    expect(i18n.t).toHaveBeenCalledWith("manufacturing.productionBatchStatus.Started", "Started");
  });

  it("returns an empty label for an empty backend value", () => {
    const i18n = { t: jasmine.createSpy("t") } as unknown as HisHopeI18nService;
    expect(manufacturingEnumLabel(i18n, "disposition", " ")).toBe("");
    expect(i18n.t).not.toHaveBeenCalled();
  });
});
