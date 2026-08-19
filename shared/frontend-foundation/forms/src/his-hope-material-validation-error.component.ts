import {
  ChangeDetectionStrategy,
  Component,
  input,
  inject,
} from "@angular/core";
import { AbstractControl } from "@angular/forms";
import { MatFormFieldModule } from "@angular/material/form-field";
import { HisHopeValidationMessageRegistry } from "./his-hope-validation-message-registry";

@Component({
  selector: "hh-mat-validation-error",
  standalone: true,
  imports: [MatFormFieldModule],
  changeDetection: ChangeDetectionStrategy.Default,
  template: `
    @if (message()) {
      <mat-error>{{ message() }}</mat-error>
    }
  `,
})
export class HisHopeMaterialValidationErrorComponent {
  readonly control = input.required<AbstractControl>();
  readonly messages = input<Record<string, string>>({});
  private readonly registry = inject(HisHopeValidationMessageRegistry);

  message(): string {
    return this.registry.forControl(this.control(), this.messages());
  }
}
