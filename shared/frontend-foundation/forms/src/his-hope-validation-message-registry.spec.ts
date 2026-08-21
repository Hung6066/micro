import { FormControl, Validators } from "@angular/forms";
import { HisHopeValidationMessageRegistry } from "./his-hope-validation-message-registry";

describe("HisHopeValidationMessageRegistry", () => {
  it("prefers required messages when multiple errors exist", () => {
    const registry = new HisHopeValidationMessageRegistry();
    const control = new FormControl("", Validators.required);
    control.setErrors({ required: true, httpsUri: true });
    control.markAsTouched();

    expect(registry.forControl(control)).toBe("This field is required.");
    expect(
      registry.forControl(control, {
        required: "Mã ứng dụng là bắt buộc.",
        httpsUri: "Chỉ dùng URI HTTPS.",
      }),
    ).toBe("Mã ứng dụng là bắt buộc.");
  });
});
