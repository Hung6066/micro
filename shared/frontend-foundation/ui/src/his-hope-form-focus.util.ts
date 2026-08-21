import { AbstractControl, FormArray, FormGroup } from "@angular/forms";

export interface HisHopeInvalidControlMatch {
  readonly control: AbstractControl;
  readonly path: readonly string[];
}

/** Depth-first search for the first invalid leaf control in a form tree. */
export function findFirstInvalidControl(
  control: AbstractControl,
  path: readonly string[] = [],
): HisHopeInvalidControlMatch | null {
  if (control instanceof FormGroup) {
    for (const key of Object.keys(control.controls)) {
      const child = control.controls[key];
      const match = findFirstInvalidControl(child, [...path, key]);
      if (match) return match;
    }
    return control.invalid && control.errors
      ? { control, path }
      : null;
  }

  if (control instanceof FormArray) {
    for (let index = 0; index < control.length; index += 1) {
      const match = findFirstInvalidControl(control.at(index), [
        ...path,
        String(index),
      ]);
      if (match) return match;
    }
    return control.invalid && control.errors
      ? { control, path }
      : null;
  }

  return control.invalid ? { control, path } : null;
}

/** Focus and scroll the first invalid Material field inside a form host. */
export function focusFirstInvalidControl(host: ParentNode): boolean {
  const invalidField = host.querySelector(
    ".hh-mat-field.mat-form-field-invalid, .mat-mdc-form-field.mat-form-field-invalid",
  ) as HTMLElement | null;
  if (!invalidField) return false;

  const focusTarget = invalidField.querySelector<HTMLElement>(
    "input:not([type='hidden']), textarea, mat-select, .mat-mdc-select-trigger",
  );
  focusTarget?.focus({ preventScroll: true });
  invalidField.scrollIntoView({ block: "nearest", behavior: "smooth" });
  return true;
}
