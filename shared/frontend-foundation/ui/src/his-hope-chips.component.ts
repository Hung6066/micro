import { CommonModule } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  forwardRef,
  input,
  output,
  signal,
} from "@angular/core";
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from "@angular/forms";

/**
 * Free-form tag/chip input. Implements `ControlValueAccessor` (`string[]`)
 * so it binds with `formControlName`/`[(ngModel)]`. Compose inside
 * `hh-form-field` for label/hint/error semantics.
 */
@Component({
  selector: "hh-chips",
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => HisHopeChipsComponent),
      multi: true,
    },
  ],
  host: {
    class: "hh-chips",
    role: "list",
    "[attr.aria-label]": "label()",
    "[attr.aria-readonly]": "readonly()",
    "[attr.aria-disabled]": "disabled()",
  },
  template: `
    @for (chip of value(); track chip; let i = $index) {
      <span class="hh-chips__chip" role="listitem">
        <span>{{ chip }}</span>
        @if (!disabled() && !readonly()) {
          <button
            type="button"
            class="hh-chips__remove"
            [attr.aria-label]="removeLabel() + ' ' + chip"
            (click)="remove(i)"
          >
            &times;
          </button>
        }
      </span>
    }
    @if (!disabled() && !readonly()) {
      <input
        #chipInput
        class="hh-chips__input"
        type="text"
        [placeholder]="value().length ? '' : placeholder()"
        [attr.aria-label]="label()"
        (keydown)="onKeydown($event, chipInput)"
        (blur)="onBlur(chipInput)"
      />
    }
  `,
  styles: [
    `
      :host {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--space-xs);
        min-height: var(--control-height);
        box-sizing: border-box;
        width: 100%;
        padding: var(--space-xs) var(--space-sm);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        color: var(--text-primary);
        transition:
          border-color var(--motion-fast) var(--ease-standard),
          background-color var(--motion-fast) var(--ease-standard),
          outline-color var(--motion-fast) var(--ease-standard);
      }
      :host(:focus-within) {
        border-color: var(--color-focus);
        outline: var(--focus-ring-width-strong) solid
          color-mix(in srgb, var(--color-primary) 20%, transparent);
      }
      :host([aria-readonly="true"]) {
        background: var(--surface-muted);
      }
      :host([aria-disabled="true"]) {
        opacity: 0.6;
      }
      .hh-chips__chip {
        display: inline-flex;
        align-items: center;
        gap: var(--space-2xs);
        padding: var(--space-hairline) var(--space-2xs) var(--space-hairline) var(--space-md);
        border-radius: var(--radius-pill);
        border: 1px solid
          color-mix(in srgb, var(--color-primary) 24%, transparent);
        background: var(--color-primary-soft, var(--surface-hover));
        color: var(--color-primary-deep, var(--text-primary));
        font-size: var(--font-size-caption);
      }
      .hh-chips__remove {
        display: grid;
        place-items: center;
        width: 20px;
        height: 20px;
        border: 0;
        border-radius: var(--radius-full);
        background: transparent;
        color: inherit;
        font-size: var(--font-size-body);
        cursor: pointer;
      }
      .hh-chips__remove:hover {
        background: var(--surface-hover);
      }
      .hh-chips__input {
        flex: 1 0 80px;
        min-width: 80px;
        border: 0;
        outline: 0;
        background: transparent;
        color: var(--text-primary);
        font: inherit;
      }
    `,
  ],
})
export class HisHopeChipsComponent implements ControlValueAccessor {
  readonly label = input("Tags");
  readonly placeholder = input("Add a tag");
  readonly removeLabel = input("Remove");
  readonly maxChips = input<number | null>(null);
  readonly readonly = input(false);
  readonly separatorKeys = input<readonly string[]>(["Enter", ","]);
  readonly chipsChange = output<string[]>();

  private readonly valueSignal = signal<string[]>([]);
  readonly value = this.valueSignal.asReadonly();
  readonly disabled = signal(false);

  private onChange: (value: string[]) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: string[] | null): void {
    this.valueSignal.set(value ?? []);
  }

  registerOnChange(fn: (value: string[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  onKeydown(event: KeyboardEvent, chipInput: HTMLInputElement): void {
    if (this.separatorKeys().includes(event.key)) {
      event.preventDefault();
      this.addFromInput(chipInput);
    } else if (
      event.key === "Backspace" &&
      !chipInput.value &&
      this.value().length
    ) {
      this.remove(this.value().length - 1);
    }
  }

  onBlur(chipInput: HTMLInputElement): void {
    this.addFromInput(chipInput);
    this.onTouched();
  }

  remove(index: number): void {
    const next = this.value().filter((_, i) => i !== index);
    this.emit(next);
  }

  private addFromInput(chipInput: HTMLInputElement): void {
    const raw = chipInput.value.trim();
    chipInput.value = "";
    if (!raw || this.value().includes(raw)) return;
    const max = this.maxChips();
    if (max !== null && this.value().length >= max) return;
    this.emit([...this.value(), raw]);
  }

  private emit(next: string[]): void {
    this.valueSignal.set(next);
    this.onChange(next);
    this.chipsChange.emit(next);
  }
}
