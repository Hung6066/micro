import {
  ChangeDetectionStrategy,
  Component,
  input,
  inject,
} from "@angular/core";
import { AbstractControl } from "@angular/forms";
import { HisHopeValidationMessageRegistry } from "./his-hope-validation-message-registry";

/** Field-level validation message rendered below the Material outline. */
@Component({
  selector: "hh-mat-validation-error",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Default,
  template: `
    @if (message()) {
      <p class="hh-field-error" role="alert">{{ message() }}</p>
    }
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-field-error {
        margin: var(--space-2xs) 0 0;
        color: var(--color-danger);
        font-size: var(--font-size-caption, 12px);
        line-height: 1.45;
      }
    `,
  ],
})
export class HisHopeMaterialValidationErrorComponent {
  readonly control = input.required<AbstractControl>();
  readonly messages = input<Record<string, string>>({});
  private readonly registry = inject(HisHopeValidationMessageRegistry);

  message(): string {
    return this.registry.forControl(this.control(), this.messages());
  }
}
