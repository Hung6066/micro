import { FormControl, FormGroup } from "@angular/forms";
import { formHasValidationFeedback } from "./his-hope-form-validation.util";

describe("formHasValidationFeedback", () => {
  it("returns false for a valid untouched form", () => {
    const form = new FormGroup({
      name: new FormControl(""),
    });
    expect(formHasValidationFeedback(form)).toBeFalse();
  });

  it("returns true after markAllAsTouched on an invalid form", () => {
    const form = new FormGroup({
      name: new FormControl("", { nonNullable: true }),
    });
    form.controls.name.setErrors({ required: true });
    form.markAllAsTouched();
    expect(formHasValidationFeedback(form)).toBeTrue();
  });
});
