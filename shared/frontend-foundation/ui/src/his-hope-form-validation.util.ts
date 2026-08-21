import { AbstractControl, FormArray, FormGroup } from "@angular/forms";

/** True when invalid controls should show submit feedback. */
export function formHasValidationFeedback(control: AbstractControl): boolean {
  if (!control.invalid) return false;
  if (control instanceof FormGroup || control instanceof FormArray) {
    return Object.values(control.controls).some(formHasValidationFeedback);
  }
  return control.touched || control.dirty;
}
