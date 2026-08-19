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
    <mat-form-field
      [appearance]="appearance()"
      [subscriptSizing]="subscriptSizing()"
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
  readonly subscriptSizing = input<"fixed" | "dynamic">("fixed");
  readonly multiline = input(false);
  readonly rows = input(2);
}
