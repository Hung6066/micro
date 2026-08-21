import { FormControl, FormGroup, Validators } from '@angular/forms';

export interface HisHopeFormFieldSchema<TValue> {
  key: string;
  label: string;
  initialValue: TValue;
  type?: "text" | "email" | "number" | "password" | "textarea" | "select";
  placeholder?: string;
  hint?: string;
  options?: readonly { value: string; label: string }[];
  disabled?: boolean;
  required?: boolean;
  hidden?: boolean;
  multiple?: boolean;
  multiline?: boolean;
  rows?: number;
  messages?: Record<string, string>;
}

export interface HisHopeFormSchema<TValue extends Record<string, unknown>> {
  fields: { [K in keyof TValue]: HisHopeFormFieldSchema<TValue[K]> };
}

export interface HisHopeFormControlContract<TValue> {
  value: TValue;
  disabled: boolean;
  touched: boolean;
  dirty: boolean;
  errors: Record<string, unknown> | null;
}

export function createHisHopeFormGroup<TValue extends Record<string, unknown>>(
  schema: HisHopeFormSchema<TValue>,
): FormGroup {
  const controls: Record<string, FormControl> = {};
  for (const field of Object.values(schema.fields)) {
    controls[field.key] = new FormControl(
      { value: field.initialValue, disabled: field.disabled ?? false },
      field.required ? Validators.required : [],
    );
  }
  return new FormGroup(controls);
}
