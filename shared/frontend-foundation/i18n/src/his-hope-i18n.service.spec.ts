import { TestBed } from "@angular/core/testing";
import { HisHopeI18nService } from "./his-hope-i18n.service";
import { hisHopeEn, hisHopeViVN } from "./dictionaries";

function leafKeys(value: unknown, prefix = ""): string[] {
  if (!value || typeof value !== "object") return prefix ? [prefix] : [];
  return Object.entries(value).flatMap(([key, child]) =>
    leafKeys(child, prefix ? `${prefix}.${key}` : key),
  );
}

describe("HisHopeI18nService", () => {
  let service: HisHopeI18nService;

  beforeEach(() => {
    localStorage.removeItem("hh-locale");
    document.documentElement.lang = "";
    TestBed.configureTestingModule({ providers: [HisHopeI18nService] });
    service = TestBed.inject(HisHopeI18nService);
  });

  afterEach(() => {
    localStorage.removeItem("hh-locale");
    document.documentElement.lang = "";
  });

  it("defaults to vi-VN when nothing is stored and the document has no explicit locale", () => {
    expect(service.locale()).toBe("vi-VN");
  });

  it("resolves a nested key from the active dictionary", () => {
    expect(service.t("common.search")).toBe("Tìm kiếm");
  });

  it("keeps operator shell and traceability labels localized in Vietnamese", () => {
    expect(service.t("customerPortal.selectWorkspace")).toBe(
      "Chọn không gian làm việc",
    );
    expect(service.t("customerPortal.eventReceipts")).toBe("Sự kiện nhập kho");
    expect(service.t("customerPortal.traceabilitySections")).toBe(
      "Các phân hệ truy xuất nguồn gốc",
    );
  });

  it("falls back to the provided fallback text for unknown keys", () => {
    expect(service.t("nope.missing", "Default text")).toBe("Default text");
  });

  it("falls back to the key itself when no fallback argument is given", () => {
    expect(service.t("nope.missing")).toBe("nope.missing");
  });

  it("ignores remote translations whose value equals the key and uses the bundled dictionary", () => {
    service.setLocale("en");
    service.registerTranslations("en-US", {
      "customerPortal.manufacturingLots": "customerPortal.manufacturingLots",
    });
    expect(service.t("customerPortal.manufacturingLots", "Lots")).toBe("Lots");
  });

  it("prefers bundled locale text when remote echoes the English dictionary", () => {
    service.setLocale("vi-VN");
    service.registerTranslations("vi-VN", {
      "customerPortal.dashboardTitle": "Dashboard",
    });
    expect(service.t("customerPortal.dashboardTitle", "Dashboard")).toBe(
      "Bảng điều khiển",
    );
  });

  it("keeps English and Vietnamese dictionaries structurally in sync", () => {
    expect(leafKeys(hisHopeViVN).sort()).toEqual(leafKeys(hisHopeEn).sort());
  });

  it("canonicalizes direct English remote registrations to the API locale", () => {
    service.setLocale("en");
    service.registerTranslations("en", {
      "customerPortal.manufacturingLots": "Manufacturing lots",
    });
    expect(service.t("customerPortal.manufacturingLots")).toBe(
      "Manufacturing lots",
    );
  });

  it("interpolates {{param}} placeholders", () => {
    service.registerLocale("test-locale", { greeting: "Hello {{name}}" });
    service.setLocale("test-locale");
    expect(service.t("greeting", "greeting", { name: "Ada" })).toBe(
      "Hello Ada",
    );
  });

  it("switches locale and persists the choice", () => {
    service.setLocale("en");
    expect(service.locale()).toBe("en");
    expect(localStorage.getItem("hh-locale")).toBe("en");
  });

  it("ignores an unregistered locale", () => {
    service.setLocale("en");
    service.setLocale("fr-does-not-exist");
    expect(service.locale()).toBe("en");
  });

  it("formats numbers using the active locale", () => {
    service.setLocale("en");
    expect(service.formatNumber(1234.5)).toBe("1,234.5");
  });

  it("exposes canonical API locale and formats currency with the selected ISO code", () => {
    service.setLocale("en");
    service.setCurrency("USD");
    expect(service.apiLocale()).toBe("en-US");
    expect(service.formatCurrency(1234.5, "USD")).toContain("1,234.50");
  });

  it("resolves a plural form and interpolates the count", () => {
    service.registerLocale("plural-test", {
      items: { one: "{{count}} item", other: "{{count}} items" },
    });
    service.setLocale("plural-test");
    expect(service.plural("items", 1)).toBe("1 item");
    expect(service.plural("items", 5)).toBe("5 items");
  });
});
