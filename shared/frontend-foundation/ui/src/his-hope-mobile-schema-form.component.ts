import { ChangeDetectionStrategy, Component, input } from "@angular/core";
import { FormControl, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { HisHopeFormFieldComponent } from "./his-hope-form-field.component";

/**
 * Field descriptor accepted by the mobile schema form. It is a structural
 * subset of `HisHopeFormFieldSchema` from the forms entry point, so a schema
 * built with `createHisHopeFormGroup` can be passed straight through. The type
 * is declared locally because the forms entry point already depends on this
 * one, and ng-packagr rejects circular entry-point references.
 */
export interface HisHopeMobileSchemaField {
  key: string;
  label: string;
  type?: "text" | "email" | "number" | "password" | "textarea" | "select";
  placeholder?: string;
  hint?: string;
  options?: readonly { value: string; label: string }[];
  required?: boolean;
  hidden?: boolean;
  multiline?: boolean;
  rows?: number;
  messages?: Record<string, string>;
}

/**
 * Renders a touch-sized field per schema entry against an existing
 * `FormGroup`. The host owns the group, validation rules, and submission.
 */
@Component({
  selector: "hh-mobile-schema-form",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    HisHopeFormFieldComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hh-mobile-schema-form" [formGroup]="form()">
      @for (field of visibleFields(); track field.key) {
        <hh-form-field
          [controlId]="controlId(field)"
          [label]="field.label"
          [hint]="field.hint ?? ''"
          [error]="
            error(field) ||
            (hasRequiredError(field)
              ? (requiredMessage() | hhTranslate: requiredMessageFallback())
              : '')
          "
          [required]="field.required ?? false"
          [disabled]="control(field).disabled"
          [dirty]="control(field).dirty"
        >
          @if (field.type === "select") {
            <select
              [id]="controlId(field)"
              [formControlName]="field.key"
              [attr.aria-required]="field.required ? 'true' : null"
            >
              @for (option of field.options ?? []; track option.value) {
                <option [value]="option.value">{{ option.label }}</option>
              }
            </select>
          } @else if (field.multiline || field.type === "textarea") {
            <textarea
              [id]="controlId(field)"
              [formControlName]="field.key"
              [rows]="field.rows ?? 3"
              [attr.placeholder]="field.placeholder || null"
              [attr.aria-required]="field.required ? 'true' : null"
            ></textarea>
          } @else {
            <input
              [id]="controlId(field)"
              [formControlName]="field.key"
              [type]="inputType(field)"
              [attr.placeholder]="field.placeholder || null"
              [attr.aria-required]="field.required ? 'true' : null"
            />
          }
        </hh-form-field>
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-mobile-schema-form {
        display: grid;
        gap: var(--form-field-gap, var(--space-lg));
      }
      input,
      textarea,
      select {
        box-sizing: border-box;
        width: 100%;
        min-height: var(--control-height-touch);
        padding: var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
        /* 16px keeps iOS Safari from zooming when a field takes focus. */
        font-size: var(--font-size-input);
      }
      textarea {
        resize: vertical;
      }
      input:focus,
      textarea:focus,
      select:focus {
        border-color: var(--color-primary);
        outline: var(--focus-ring-width-strong) solid
          color-mix(in srgb, var(--color-primary) 20%, transparent);
      }
    `,
  ],
})
export class HisHopeMobileSchemaFormComponent {
  readonly form = input.required<FormGroup>();
  readonly fields = input.required<readonly HisHopeMobileSchemaField[]>();
  readonly requiredMessage = input("validation.required");
  readonly requiredMessageFallback = input("This field is required.");

  private readonly instanceId = Math.random().toString(36).slice(2);

  visibleFields(): readonly HisHopeMobileSchemaField[] {
    return this.fields().filter((field) => !field.hidden);
  }

  control(field: HisHopeMobileSchemaField): FormControl {
    return this.form().get(field.key) as FormControl;
  }

  controlId(field: HisHopeMobileSchemaField): string {
    return `hh-mobile-schema-${this.instanceId}-${field.key}`;
  }

  inputType(
    field: HisHopeMobileSchemaField,
  ): "text" | "email" | "number" | "password" {
    if (field.type === "email") return "email";
    if (field.type === "number") return "number";
    if (field.type === "password") return "password";
    return "text";
  }

  error(field: HisHopeMobileSchemaField): string {
    if (!this.showsFeedback(field)) return "";
    const errorKey = Object.keys(this.control(field).errors ?? {})[0];
    return field.messages?.[errorKey] ?? "";
  }

  hasRequiredError(field: HisHopeMobileSchemaField): boolean {
    return this.showsFeedback(field) && !!this.control(field).errors?.["required"];
  }

  private showsFeedback(field: HisHopeMobileSchemaField): boolean {
    const control = this.control(field);
    return !!control?.invalid && (control.touched || control.dirty);
  }
}
