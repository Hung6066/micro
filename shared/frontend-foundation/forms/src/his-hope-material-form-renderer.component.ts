import { ChangeDetectionStrategy, Component, input } from "@angular/core";
import { FormControl, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { HisHopeMaterialFormFieldComponent } from "./his-hope-material-form-field.component";
import { HisHopeFormFieldSchema } from "./his-hope-form-schema";

/** Renders reactive form controls with Material field chrome from a field schema. */
@Component({
  selector: "hh-material-form-renderer",
  standalone: true,
  imports: [ReactiveFormsModule, HisHopeMaterialFormFieldComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hh-material-form-renderer">
      @for (field of fields(); track field.key) {
        @if (!field.hidden) {
          <hh-mat-form-field
            [control]="control(field)"
            [label]="field.label"
            [hint]="field.hint ?? ''"
            [placeholder]="field.placeholder ?? ''"
            [kind]="field.type === 'select' ? 'select' : 'text'"
            [type]="inputType(field)"
            [options]="field.options ?? []"
            [multiple]="field.multiple ?? false"
            [multiline]="field.multiline ?? field.type === 'textarea'"
            [rows]="field.rows ?? 2"
            [messages]="field.messages ?? {}"
          />
        }
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-material-form-renderer {
        display: grid;
        gap: var(--form-field-gap, 16px);
      }
    `,
  ],
})
export class HisHopeMaterialFormRendererComponent {
  readonly fields =
    input.required<readonly HisHopeFormFieldSchema<unknown>[]>();
  readonly form = input.required<FormGroup>();

  control(field: HisHopeFormFieldSchema<unknown>): FormControl {
    return this.form().get(field.key) as FormControl;
  }

  inputType(
    field: HisHopeFormFieldSchema<unknown>,
  ): "text" | "email" | "number" | "password" {
    if (field.type === "email") return "email";
    if (field.type === "number") return "number";
    if (field.type === "password") return "password";
    return "text";
  }
}
