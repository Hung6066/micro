import { operatorMobileErrorMessage } from "./operator-mobile-error.util";

describe("operatorMobileErrorMessage", () => {
  const i18n = { t: (key: string, fallback: string) => `${key}:${fallback}` };

  it("localizes authorization and conflict statuses", () => {
    expect(operatorMobileErrorMessage(i18n, { status: 401 })).toContain("operatorSessionExpired");
    expect(operatorMobileErrorMessage(i18n, { status: 403 })).toContain("operatorForbidden");
    expect(operatorMobileErrorMessage(i18n, { status: 409 })).toContain("operatorConflict");
  });

  it("maps validation problem codes to validation guidance", () => {
    expect(operatorMobileErrorMessage(i18n, { error: { errorCode: "invalid_operation_measurement" } })).toContain("operatorValidationFailed");
  });
});
