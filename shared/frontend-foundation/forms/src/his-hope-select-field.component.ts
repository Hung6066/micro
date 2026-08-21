import { ChangeDetectionStrategy, Component, input } from "@angular/core";
import { AbstractControl, ReactiveFormsModule } from "@angular/forms";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatSelectModule } from "@angular/material/select";
import { HisHopeMaterialValidationErrorComponent } from "./his-hope-material-validation-error.component";

export interface HisHopeSelectOption {
  value: string;
  label: string;
}

@Component({
  selector: "hh-select-field",
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule,
    MatSelectModule,
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
        <mat-select
          class="hh-select"
          [class.hh-select--multiple]="multiple()"
          [formControl]="$any(control())"
          [multiple]="multiple()"
        >
          @for (option of options(); track option.value) {
            <mat-option [value]="option.value">{{ option.label }}</mat-option>
          }
        </mat-select>
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

      :host ::ng-deep .hh-select--multiple .mat-mdc-select-value,
      :host ::ng-deep .hh-select--multiple .mat-mdc-select-value-text {
        white-space: normal;
        overflow-wrap: anywhere;
      }

      :host ::ng-deep .hh-select--multiple .mat-mdc-select-trigger {
        min-height: 24px;
        height: auto;
        align-items: flex-start;
      }

      :host ::ng-deep .hh-select--multiple .mat-mdc-select-arrow-wrapper {
        align-self: flex-start;
        margin-top: var(--space-2xs);
      }

      :host ::ng-deep .hh-select .mat-mdc-select-arrow-wrapper {
        width: 24px;
        height: 24px;
      }

      :host ::ng-deep .hh-select .mat-mdc-select-arrow {
        width: var(--size-timeline-rail);
        height: 9px;
      }

      :host ::ng-deep .hh-select .mat-mdc-select-arrow svg {
        transform: translate(-50%, -50%) scale(1.6);
      }

      :host ::ng-deep .mat-mdc-option .mdc-list-item__primary-text {
        white-space: normal;
        overflow-wrap: anywhere;
      }
    `,
  ],
})
export class HisHopeSelectFieldComponent {
  readonly control = input.required<AbstractControl>();
  readonly options = input.required<ReadonlyArray<HisHopeSelectOption>>();
  readonly label = input("");
  readonly hint = input("");
  readonly messages = input<Record<string, string>>({});
  readonly appearance = input<"fill" | "outline">("outline");
  readonly subscriptSizing = input<"fixed" | "dynamic">("dynamic");
  readonly multiple = input(false);

  showError(): boolean {
    const control = this.control();
    return !!control.errors && (control.touched || control.dirty);
  }
}
