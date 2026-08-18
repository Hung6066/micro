import {
  clampColumnWidth,
  compareRowsBySortTerms,
  isResponsiveColumnHidden,
  operatorLabel,
  orderIndexOf,
} from "./his-hope-data-table.helpers";

describe("his-hope-data-table.helpers", () => {
  it("clampColumnWidth respects min/max bounds and rounds", () => {
    expect(clampColumnWidth(50, { minWidth: 96, maxWidth: 640 })).toBe(96);
    expect(clampColumnWidth(1000, { minWidth: 96, maxWidth: 640 })).toBe(640);
    expect(clampColumnWidth(200.4, {})).toBe(200);
  });

  it("operatorLabel maps known operators and falls back to the raw value", () => {
    expect(operatorLabel("eq")).toBe("=");
    expect(operatorLabel("contains")).toBe("∋");
    expect(operatorLabel("unknown")).toBe("unknown");
  });

  it("orderIndexOf returns the position or the order length when missing", () => {
    expect(orderIndexOf(["a", "b", "c"], "b")).toBe(1);
    expect(orderIndexOf(["a", "b", "c"], "z")).toBe(3);
  });

  it("compareRowsBySortTerms sorts by the first differing term and respects direction", () => {
    const rows = [{ name: "b" }, { name: "a" }];
    expect(
      compareRowsBySortTerms(rows[0], rows[1], [
        { key: "name", direction: "asc" },
      ]),
    ).toBeGreaterThan(0);
    expect(
      compareRowsBySortTerms(rows[0], rows[1], [
        { key: "name", direction: "desc" },
      ]),
    ).toBeLessThan(0);
    expect(
      compareRowsBySortTerms({ name: "a" }, { name: "a" }, [
        { key: "name", direction: "asc" },
      ]),
    ).toBe(0);
  });

  it("isResponsiveColumnHidden hides higher-priority columns at narrower viewports", () => {
    expect(isResponsiveColumnHidden(undefined, 320)).toBe(false);
    expect(isResponsiveColumnHidden(1, 320)).toBe(false);
    expect(isResponsiveColumnHidden(2, 320)).toBe(true);
    expect(isResponsiveColumnHidden(3, 900)).toBe(true);
    expect(isResponsiveColumnHidden(3, 1200)).toBe(false);
  });
});
