import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Output,
  inject,
  input,
} from "@angular/core";
import { FormControl, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { HisHopeFormFieldComponent } from "@his-hope/frontend-foundation/ui";
import { HisHopeFormFieldSchema } from "./his-hope-form-schema";
import { HisHopeValidationMessageRegistry } from "./his-hope-validation-message-registry";

@Component({
  selector: "hh-form-renderer",
  standalone: true,
  imports: [ReactiveFormsModule, HisHopeFormFieldComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form [formGroup]="form()" (ngSubmit)="submit()" novalidate>
      <div class="hh-form-renderer__fields">
        @for (field of fields(); track field.key) {
          <hh-form-field
            [controlId]="field.key"
            [label]="field.label"
            [hint]="field.hint ?? ''"
            [required]="field.required ?? false"
            [error]="errorFor(field)"
            [dirty]="control(field).dirty"
          >
            @switch (field.type ?? "text") {
              @case ("textarea") {
                <textarea
                  [id]="field.key"
                  [formControl]="control(field)"
                  [placeholder]="field.placeholder ?? ''"
                ></textarea>
              }
              @case ("select") {
                <select [id]="field.key" [formControl]="control(field)">
                  @for (option of field.options ?? []; track option.value) {
                    <option [value]="option.value">{{ option.label }}</option>
                  }
                </select>
              }
              @default {
                <input
                  [id]="field.key"
                  [type]="field.type ?? 'text'"
                  [formControl]="control(field)"
                  [placeholder]="field.placeholder ?? ''"
                />
              }
            }
          </hh-form-field>
        }
      </div>
      <ng-content />
    </form>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-form-renderer__fields {
        display: grid;
        gap: var(--form-field-gap, 16px);
      }
      input,
      textarea,
      select {
        box-sizing: border-box;
        min-height: var(--control-height, 40px);
        padding: var(--space-sm) var(--space-inset);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-input);
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
      }
      textarea {
        min-height: var(--size-empty-state-icon);
        resize: vertical;
      }
    `,
  ],
})
export class HisHopeFormRendererComponent {
  readonly fields =
    input.required<readonly HisHopeFormFieldSchema<unknown>[]>();
  readonly form = input.required<FormGroup>();
  @Output() readonly submitted = new EventEmitter<Record<string, unknown>>();
  private readonly messages = inject(HisHopeValidationMessageRegistry);

  control(field: HisHopeFormFieldSchema<unknown>): FormControl {
    return this.form().get(field.key) as FormControl;
  }

  errorFor(field: HisHopeFormFieldSchema<unknown>): string {
    return this.messages.forControl(this.control(field), field.messages ?? {});
  }

  submit(): void {
    this.form().markAllAsTouched();
    if (this.form().valid) this.submitted.emit(this.form().getRawValue());
  }
}
