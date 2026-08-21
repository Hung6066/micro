import { ChangeDetectionStrategy, Component, input } from "@angular/core";
import { AbstractControl, ReactiveFormsModule } from "@angular/forms";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { HisHopeMaterialValidationErrorComponent } from "./his-hope-material-validation-error.component";

@Component({
  selector: "hh-text-field",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    HisHopeMaterialValidationErrorComponent,
  ],
  changeDetection: ChangeDetectionStrategy.Default,
  template: `
    <div class="hh-field-shell">
      <mat-form-field
        class="hh-mat-field"
        [appearance]="appearance()"
        subscriptSizing="dynamic"
      >
        @if (label()) {
          <mat-label>{{ label() }}</mat-label>
        }
        @if (multiline()) {
          <textarea
            matInput
            [formControl]="$any(control())"
            [rows]="rows()"
            [placeholder]="placeholder()"
          ></textarea>
        } @else {
          <input
            matInput
            [type]="type()"
            [formControl]="$any(control())"
            [placeholder]="placeholder()"
          />
        }
      </mat-form-field>
      <hh-mat-validation-error [control]="control()" [messages]="messages()" />
      @if (hint() && !showError()) {
        <p class="hh-field-hint">{{ hint() }}</p>
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
        width: 100%;
      }
      .hh-field-shell {
        display: grid;
        gap: 0;
        width: 100%;
      }
      .hh-field-hint {
        margin: var(--space-2xs) 0 0;
        color: var(--text-muted, #667085);
        font-size: var(--font-size-caption, 12px);
        line-height: 1.45;
      }
      :host ::ng-deep .hh-mat-field .mat-mdc-form-field-subscript-wrapper {
        display: none;
      }
    `,
  ],
})
export class HisHopeTextFieldComponent {
  readonly control = input.required<AbstractControl>();
  readonly label = input("");
  readonly hint = input("");
  readonly placeholder = input("");
  readonly type = input<"text" | "email" | "number" | "password">("text");
  readonly messages = input<Record<string, string>>({});
  readonly appearance = input<"fill" | "outline">("outline");
  readonly subscriptSizing = input<"fixed" | "dynamic">("dynamic");
  readonly multiline = input(false);
  readonly rows = input(2);

  showError(): boolean {
    const control = this.control();
    return !!control.errors && (control.touched || control.dirty);
  }
}
