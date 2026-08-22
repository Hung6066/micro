import { CommonModule } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  forwardRef,
  inject,
  input,
  output,
  signal,
} from "@angular/core";
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from "@angular/forms";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

/**
 * Free-form tag/chip input. Implements `ControlValueAccessor` (`string[]`)
 * so it binds with `formControlName`/`[(ngModel)]`. Compose inside
 * `hh-form-field` for label/hint/error semantics.
 */
@Component({
  selector: "hh-chips",
  standalone: true,
  imports: [CommonModule, HisHopeTranslatePipe],
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
    "[attr.aria-label]": "labelText()",
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
            [attr.aria-label]="removeAriaLabel(chip)"
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
        [placeholder]="value().length ? '' : (placeholder() | hhTranslate)"
        [attr.aria-label]="labelText()"
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
          box-shadow var(--motion-fast) var(--ease-standard);
      }
      :host(:focus-within) {
        border-color: var(--color-primary);
        box-shadow: 0 0 0 3px
          color-mix(in srgb, var(--color-primary) 16%, transparent);
      }
      .hh-chips__chip {
        display: inline-flex;
        align-items: center;
        gap: var(--space-2xs);
        padding: var(--space-2xs) var(--space-sm);
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
  private readonly i18n = inject(HisHopeI18nService);

  readonly label = input("common.tags");
  readonly placeholder = input("common.addTag");
  readonly removeLabel = input("common.delete");
  readonly maxChips = input<number | null>(null);
  readonly readonly = input(false);
  readonly separatorKeys = input<readonly string[]>(["Enter", ","]);
  readonly chipsChange = output<string[]>();
  readonly labelText = computed(() => this.i18n.t(this.label(), this.label()));

  private readonly valueSignal = signal<string[]>([]);
  readonly value = this.valueSignal.asReadonly();
  readonly disabled = signal(false);

  private onChange: (value: string[]) => void = () => {};
  private onTouched: () => void = () => {};

  removeAriaLabel(chip: string): string {
    return `${this.i18n.t(this.removeLabel(), this.removeLabel())} ${chip}`;
  }

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
    this.valueSignal.set(next);
    this.onChange(next);
    this.chipsChange.emit(next);
  }

  private addFromInput(chipInput: HTMLInputElement): void {
    const raw = chipInput.value.trim();
    chipInput.value = "";
    if (!raw) return;
    const max = this.maxChips();
    const current = this.value();
    if (max !== null && current.length >= max) return;
    if (current.includes(raw)) return;
    const next = [...current, raw];
    this.valueSignal.set(next);
    this.onChange(next);
    this.chipsChange.emit(next);
  }
}
