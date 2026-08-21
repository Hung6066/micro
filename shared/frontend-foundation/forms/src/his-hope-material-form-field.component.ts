import { ChangeDetectionStrategy, Component, input } from "@angular/core";
import { AbstractControl } from "@angular/forms";
import { HisHopeSelectFieldComponent } from "./his-hope-select-field.component";
import { HisHopeTextFieldComponent } from "./his-hope-text-field.component";

@Component({
  selector: "hh-mat-form-field",
  standalone: true,
  imports: [HisHopeTextFieldComponent, HisHopeSelectFieldComponent],
  changeDetection: ChangeDetectionStrategy.Default,
  template: `
    @if (kind() === "select") {
      <hh-select-field
        [control]="control()"
        [options]="options()"
        [label]="label()"
        [hint]="hint()"
        [messages]="messages()"
        [appearance]="appearance()"
        [subscriptSizing]="subscriptSizing()"
        [multiple]="multiple()"
      />
    } @else {
      <hh-text-field
        [control]="control()"
        [label]="label()"
        [hint]="hint()"
        [placeholder]="placeholder()"
        [type]="type()"
        [messages]="messages()"
        [appearance]="appearance()"
        [subscriptSizing]="subscriptSizing()"
        [multiline]="multiline()"
        [rows]="rows()"
      />
    }
  `,
})
export class HisHopeMaterialFormFieldComponent {
  readonly control = input.required<AbstractControl>();
  readonly kind = input<"text" | "select">("text");
  readonly options = input<ReadonlyArray<{ value: string; label: string }>>([]);
  readonly label = input("");
  readonly hint = input("");
  readonly placeholder = input("");
  readonly type = input<"text" | "email" | "number" | "password">("text");
  readonly messages = input<Record<string, string>>({});
  readonly appearance = input<"fill" | "outline">("outline");
  readonly subscriptSizing = input<"fixed" | "dynamic">("dynamic");
  readonly multiple = input(false);
  readonly multiline = input(false);
  readonly rows = input(2);
}
