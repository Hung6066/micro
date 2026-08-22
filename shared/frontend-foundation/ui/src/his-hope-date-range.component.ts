import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  forwardRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { HisHopeI18nService } from '@his-hope/frontend-foundation/i18n';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

export interface HisHopeDateRange {
  start: string | null;
  end: string | null;
}

/**
 * Native two-field date range picker (`<input type="date">`), validated so
 * the end date cannot precede the start date. Implements
 * `ControlValueAccessor` over `HisHopeDateRange`.
 */
@Component({
  selector: 'hh-date-range',
  standalone: true,
  imports: [CommonModule, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => HisHopeDateRangeComponent),
      multi: true,
    },
  ],
  template: `
    <div class="hh-date-range" role="group" [attr.aria-label]="labelText()">
      <label class="hh-date-range__field">
        <span>{{ startLabel() | hhTranslate }}</span>
        <input
          type="date"
          [value]="value().start ?? ''"
          [min]="min() || null"
          [max]="value().end || max() || null"
          [disabled]="disabled()"
          (change)="onStartChange($any($event.target).value)"
        />
      </label>
      <span class="hh-date-range__sep" aria-hidden="true">&ndash;</span>
      <label class="hh-date-range__field">
        <span>{{ endLabel() | hhTranslate }}</span>
        <input
          type="date"
          [value]="value().end ?? ''"
          [min]="value().start || min() || null"
          [max]="max() || null"
          [disabled]="disabled()"
          (change)="onEndChange($any($event.target).value)"
        />
      </label>
    </div>
    @if (error()) {
      <p class="hh-date-range__error" role="alert">{{ errorText() }}</p>
    }
  `,
  styles: [
    `
      .hh-date-range {
        display: flex;
        align-items: flex-end;
        gap: var(--space-sm);
      }
      .hh-date-range__field {
        display: grid;
        flex: 1;
        gap: var(--space-xs);
        min-width: 0;
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
        font-weight: var(--font-weight-semibold);
      }
      .hh-date-range__field input {
        box-sizing: border-box;
        width: 100%;
        min-height: var(--control-height);
        padding: 0 var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
      }
      .hh-date-range__field input:focus-visible {
        border-color: var(--color-primary);
        outline: var(--focus-ring-width-strong) solid color-mix(in srgb, var(--color-primary) 20%, transparent);
      }
      .hh-date-range__sep {
        padding-bottom: var(--space-md);
        color: var(--text-muted);
      }
      .hh-date-range__error {
        margin: var(--space-xs) 0 0;
        color: var(--color-danger, #b91c1c);
        font-size: var(--font-size-caption);
      }
    `,
  ],
})
export class HisHopeDateRangeComponent implements ControlValueAccessor {
  private readonly i18n = inject(HisHopeI18nService);

  readonly label = input('common.dateRange');
  readonly startLabel = input('common.from');
  readonly endLabel = input('common.to');
  readonly min = input('');
  readonly max = input('');
  readonly invalidRangeMessage = input('common.invalidDateRange');
  readonly rangeChange = output<HisHopeDateRange>();

  readonly labelText = computed(() => this.i18n.t(this.label(), this.label()));

  private readonly valueSignal = signal<HisHopeDateRange>({ start: null, end: null });
  readonly value = this.valueSignal.asReadonly();
  readonly disabled = signal(false);

  readonly error = computed(() => {
    const { start, end } = this.value();
    return start && end && start > end;
  });
  readonly errorText = computed(() =>
    this.error()
      ? this.i18n.t(this.invalidRangeMessage(), this.invalidRangeMessage())
      : '',
  );

  private onChange: (value: HisHopeDateRange) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: HisHopeDateRange | null): void {
    this.valueSignal.set(value ?? { start: null, end: null });
  }

  registerOnChange(fn: (value: HisHopeDateRange) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  onStartChange(value: string): void {
    this.emit({ ...this.value(), start: value || null });
  }

  onEndChange(value: string): void {
    this.emit({ ...this.value(), end: value || null });
  }

  private emit(next: HisHopeDateRange): void {
    this.valueSignal.set(next);
    this.onChange(next);
    this.rangeChange.emit(next);
    this.onTouched();
  }
}
