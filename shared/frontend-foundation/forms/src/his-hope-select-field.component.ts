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
    <mat-form-field
      [appearance]="appearance()"
      [subscriptSizing]="subscriptSizing()"
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
      @if (hint()) {
        <mat-hint>{{ hint() }}</mat-hint>
      }
      <hh-mat-validation-error [control]="control()" [messages]="messages()" />
    </mat-form-field>
  `,
  styles: [
    `
      :host,
      mat-form-field {
        display: block;
        width: 100%;
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
        margin-top: 4px;
      }

      :host ::ng-deep .hh-select .mat-mdc-select-arrow-wrapper {
        width: 24px;
        height: 24px;
      }

      :host ::ng-deep .hh-select .mat-mdc-select-arrow {
        width: 16px;
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
  readonly subscriptSizing = input<"fixed" | "dynamic">("fixed");
  readonly multiple = input(false);
}
