import { Injectable } from '@angular/core';

export type HisHopeValidationMessage = string | ((value: unknown) => string);

@Injectable({ providedIn: 'root' })
export class HisHopeValidationMessageRegistry {
  private readonly messages = new Map<string, HisHopeValidationMessage>([
    ['required', 'This field is required.'],
    ['email', 'Enter a valid email address.'],
    ['minlength', 'Enter more characters.'],
    ['maxlength', 'Enter fewer characters.'],
  ]);

  register(key: string, message: HisHopeValidationMessage): void { this.messages.set(key, message); }
  resolve(key: string, value?: unknown): string { const message = this.messages.get(key); return typeof message === 'function' ? message(value) : message ?? key; }
  first(errors: Record<string, unknown> | null | undefined): string | null { const key = errors ? Object.keys(errors)[0] : undefined; return key ? this.resolve(key, errors?.[key]) : null; }
}