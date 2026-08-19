import { Injectable, inject } from "@angular/core";
import { AbstractControl } from "@angular/forms";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";

export type HisHopeValidationMessage = string | ((value: unknown) => string);

@Injectable({ providedIn: "root" })
export class HisHopeValidationMessageRegistry {
  private readonly i18n = inject(HisHopeI18nService);
  private readonly messages = new Map<string, HisHopeValidationMessage>([
    ["required", "This field is required."],
    ["email", "Enter a valid email address."],
    ["minlength", "Enter more characters."],
    ["maxlength", "Enter fewer characters."],
  ]);

  register(key: string, message: HisHopeValidationMessage): void {
    this.messages.set(key, message);
  }
  resolve(key: string, value?: unknown): string {
    const message = this.messages.get(key);
    if (typeof message === "function") return message(value);
    return this.i18n.t(`validation.${key}`, message ?? key);
  }
  first(errors: Record<string, unknown> | null | undefined): string | null {
    const key = errors ? Object.keys(errors)[0] : undefined;
    return key ? this.resolve(key, errors?.[key]) : null;
  }

  forControl(
    control: AbstractControl,
    overrides: Record<string, string> = {},
  ): string {
    if ((!control.touched && !control.dirty) || !control.errors) return "";
    const key = Object.keys(control.errors)[0];
    return overrides[key] ?? this.resolve(key, control.errors[key]);
  }
}
