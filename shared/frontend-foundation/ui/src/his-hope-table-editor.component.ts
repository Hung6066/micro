import { ChangeDetectionStrategy, Component, EventEmitter, forwardRef, input, Output } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { HisHopeTableEditor, HisHopeTableEditorOption } from '@his-hope/frontend-foundation/contracts';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

@Component({
  selector: 'hh-table-editor',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => HisHopeTableEditorComponent), multi: true }],
  template: `
    @if (type() === 'select') {
        <select class="hh-data-table__edit-input" [value]="displayValue()" [disabled]="disabled" (change)="changed($event)" (blur)="touched()" [attr.aria-label]="ariaLabel() | hhTranslate" [attr.aria-invalid]="invalid() ? 'true' : null">
        @for (option of options(); track option.value) { <option [value]="option.value" [disabled]="option.disabled">{{ option.label }}</option> }
      </select>
    } @else {
      <input class="hh-data-table__edit-input" [type]="type() === 'number' ? 'number' : type() === 'date' ? 'date' : 'text'" [value]="displayValue()" [disabled]="disabled" (input)="changed($event)" (blur)="touched()" [attr.list]="type() === 'autocomplete' ? listId() : null" [attr.aria-label]="ariaLabel() | hhTranslate" [attr.aria-invalid]="invalid() ? 'true' : null" />
      @if (type() === 'autocomplete') { <datalist [id]="listId()">@for (option of options(); track option.value) { <option [value]="option.value">{{ option.label }}</option> }</datalist> }
    }
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .hh-data-table__edit-input { display: block; width: 100%; min-height: var(--control-height-compact); box-sizing: border-box; border: 1px solid var(--border-default); border-radius: var(--radius-input); padding: 0 var(--space-md); background: var(--surface-white); color: var(--text-primary); font: inherit; line-height: 1.4; }
    .hh-data-table__edit-input[aria-invalid="true"] { border-color: var(--color-danger); }
    .hh-data-table__edit-input:focus { outline: var(--focus-ring-width) solid var(--color-focus); outline-offset: var(--focus-ring-offset-tight); }
    .hh-data-table__edit-input:disabled { background: var(--surface-muted); color: var(--text-muted); cursor: not-allowed; }
  `],
})
export class HisHopeTableEditorComponent implements ControlValueAccessor {
  readonly type = input<HisHopeTableEditor>('text');
  readonly options = input<HisHopeTableEditorOption[]>([]);
  readonly ariaLabel = input('common.editValue');
  readonly invalid = input(false);
  readonly listId = input('hh-table-editor-options');
  readonly value = input('');
  @Output() readonly valueChange = new EventEmitter<HisHopeTableEditorValue>();
  private cvaValue: HisHopeTableEditorValue | null = null;
  disabled = false;
  private onChange: (value: HisHopeTableEditorValue) => void = () => undefined;
  private onTouched: () => void = () => undefined;
  displayValue(): string { return this.cvaValue === null ? String(this.value() ?? '') : String(this.cvaValue); }
  writeValue(value: unknown): void { this.cvaValue = toHisHopeTableEditorValue(value, this.type()); }
  registerOnChange(fn: (value: HisHopeTableEditorValue) => void): void { this.onChange = fn; }
  registerOnTouched(fn: () => void): void { this.onTouched = fn; }
  setDisabledState(disabled: boolean): void { this.disabled = disabled; }
  touched(): void { this.onTouched(); }
  changed(event: Event): void { this.cvaValue = toHisHopeTableEditorValue((event.target as HTMLInputElement | HTMLSelectElement).value, this.type()); this.onChange(this.cvaValue); this.onTouched(); this.valueChange.emit(this.cvaValue); }
}

export type HisHopeTableEditorValue = string | number | null;

export function toHisHopeTableEditorValue(value: unknown, type: HisHopeTableEditor): HisHopeTableEditorValue {
  if (type === 'number') {
    if (value === '' || value === null || value === undefined) return null;
    const numberValue = Number(value);
    return Number.isFinite(numberValue) ? numberValue : null;
  }
  return value === null || value === undefined ? '' : String(value);
}
