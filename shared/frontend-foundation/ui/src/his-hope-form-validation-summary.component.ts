import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from "@angular/core";
import { FormGroup } from "@angular/forms";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { formHasValidationFeedback } from "./his-hope-form-validation.util";

/** Dialog/form-level validation banner shown after a failed submit attempt. */
@Component({
  selector: "hh-form-validation-summary",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (visible()) {
      <p class="hh-form-validation-summary" role="alert" aria-live="polite">
        {{ messageKey() | hhTranslate: messageFallback() }}
      </p>
    }
  `,
  styles: [
    `
      .hh-form-validation-summary {
        margin: 0;
        padding: var(--space-md);
        border: 1px solid color-mix(in srgb, var(--color-danger) 35%, transparent);
        border-radius: var(--radius-input, var(--radius-card));
        background: color-mix(in srgb, var(--color-danger) 8%, transparent);
        color: var(--color-danger);
        font-size: var(--font-size-body);
        line-height: var(--leading-body);
      }
    `,
  ],
})
export class HisHopeFormValidationSummaryComponent {
  readonly form = input.required<FormGroup>();
  readonly messageKey = input("errors.validationFailed");
  readonly messageFallback = input(
    "Validation failed. Please check your input.",
  );

  readonly visible = computed(() => formHasValidationFeedback(this.form()));
}
