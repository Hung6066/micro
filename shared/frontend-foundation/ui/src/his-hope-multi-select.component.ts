import { ConnectedPosition, Overlay, OverlayRef } from "@angular/cdk/overlay";
import { TemplatePortal } from "@angular/cdk/portal";
import { CommonModule } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  TemplateRef,
  ViewChild,
  ViewContainerRef,
  computed,
  forwardRef,
  inject,
  input,
  output,
  signal,
} from "@angular/core";
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from "@angular/forms";

export interface HisHopeMultiSelectOption<T = string> {
  value: T;
  label: string;
  disabled?: boolean;
}

const PANEL_POSITIONS: ConnectedPosition[] = [
  {
    originX: "start",
    originY: "bottom",
    overlayX: "start",
    overlayY: "top",
    offsetY: 4,
  },
  {
    originX: "start",
    originY: "top",
    overlayX: "start",
    overlayY: "bottom",
    offsetY: -4,
  },
];

/**
 * Accessible multi-select combobox (`aria-multiselectable` listbox overlay).
 * Implements `ControlValueAccessor` over `T[]`. Selecting an option keeps the
 * panel open; close with Escape, backdrop click or the trigger.
 */
@Component({
  selector: "hh-multi-select",
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => HisHopeMultiSelectComponent),
      multi: true,
    },
  ],
  host: {
    class: "hh-multi-select",
    role: "combobox",
    "[attr.aria-expanded]": "isOpen()",
    "[attr.aria-disabled]": "disabled()",
    "[attr.aria-label]": "label()",
    "[attr.tabindex]": "disabled() ? -1 : 0",
    "(click)": "toggle()",
    "(keydown)": "onTriggerKeydown($event)",
    "(blur)": "onBlur()",
  },
  template: `
    <span
      class="hh-multi-select__value"
      [class.hh-multi-select__value--placeholder]="!summaryLabel()"
    >
      {{ summaryLabel() || placeholder() }}
    </span>
    <span class="hh-multi-select__caret material-icons" aria-hidden="true"
      >expand_more</span
    >
    <ng-template #panelTemplate>
      <ul
        #panel
        class="hh-multi-select__panel"
        role="listbox"
        aria-multiselectable="true"
        [attr.aria-label]="label()"
        (keydown)="onPanelKeydown($event)"
      >
        @for (option of options(); track option.value) {
          <li
            role="option"
            class="hh-multi-select__option"
            [class.hh-multi-select__option--active]="
              option.value === activeValue()
            "
            [attr.aria-selected]="isSelected(option.value)"
            [attr.aria-disabled]="option.disabled"
            (click)="toggleOption(option)"
            (mouseenter)="activeValue.set(option.value)"
          >
            <span
              class="hh-multi-select__checkbox"
              [class.hh-multi-select__checkbox--checked]="
                isSelected(option.value)
              "
              aria-hidden="true"
            ></span>
            {{ option.label }}
          </li>
        } @empty {
          <li class="hh-multi-select__empty" role="presentation">
            {{ emptyLabel() }}
          </li>
        }
      </ul>
    </ng-template>
  `,
  styles: [
    `
      :host {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 8px;
        min-height: var(--control-height);
        box-sizing: border-box;
        width: 100%;
        padding: 0 12px;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
        cursor: pointer;
      }
      :host(:focus-visible) {
        border-color: var(--color-primary);
        outline: 3px solid
          color-mix(in srgb, var(--color-primary) 20%, transparent);
      }
      :host([aria-disabled="true"]) {
        cursor: not-allowed;
        opacity: 0.6;
      }
      .hh-multi-select__value--placeholder {
        color: var(--text-muted);
      }
      .hh-multi-select__caret {
        color: var(--text-secondary);
        font-size: 20px;
      }
      .hh-multi-select__panel {
        margin: 0;
        min-width: 220px;
        max-height: min(320px, 60vh);
        overflow: auto;
        padding: 4px;
        list-style: none;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        box-shadow: var(--shadow-dropdown);
      }
      .hh-multi-select__option {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 8px 10px;
        border-radius: var(--radius-card);
        cursor: pointer;
      }
      .hh-multi-select__option--active {
        background: var(--surface-hover);
      }
      .hh-multi-select__option[aria-disabled="true"] {
        color: var(--text-muted);
        cursor: not-allowed;
      }
      .hh-multi-select__checkbox {
        width: 16px;
        height: 16px;
        border: 1px solid var(--border-default);
        border-radius: 4px;
        background: var(--surface-white);
      }
      .hh-multi-select__checkbox--checked {
        border-color: var(--color-primary);
        background: var(--color-primary);
      }
      .hh-multi-select__empty {
        padding: 10px;
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
        text-align: center;
      }
    `,
  ],
})
export class HisHopeMultiSelectComponent<T = string>
  implements ControlValueAccessor, OnDestroy
{
  readonly options = input<HisHopeMultiSelectOption<T>[]>([]);
  readonly label = input("Select");
  readonly placeholder = input("Select options");
  readonly emptyLabel = input("No options available");
  readonly valueChange = output<T[]>();

  @ViewChild("panelTemplate")
  private readonly panelTemplate!: TemplateRef<unknown>;

  private readonly overlay = inject(Overlay);
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private readonly viewContainerRef = inject(ViewContainerRef);
  private overlayRef: OverlayRef | null = null;

  private readonly valueSignal = signal<T[]>([]);
  readonly value = this.valueSignal.asReadonly();
  readonly activeValue = signal<T | null>(null);
  private readonly isOpenSignal = signal(false);
  readonly isOpen = this.isOpenSignal.asReadonly();
  readonly disabled = signal(false);

  readonly summaryLabel = computed(() => {
    const selected = this.options().filter((option) =>
      this.valueSignal().includes(option.value),
    );
    if (!selected.length) return "";
    if (selected.length <= 2)
      return selected.map((option) => option.label).join(", ");
    return `${selected.length} selected`;
  });

  private onChange: (value: T[]) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: T[] | null): void {
    this.valueSignal.set(value ?? []);
  }

  registerOnChange(fn: (value: T[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  isSelected(value: T): boolean {
    return this.valueSignal().includes(value);
  }

  toggle(): void {
    if (this.disabled()) return;
    this.isOpenSignal() ? this.close() : this.open();
  }

  open(): void {
    if (this.overlayRef || this.disabled()) return;
    this.activeValue.set(this.options()[0]?.value ?? null);
    const positionStrategy = this.overlay
      .position()
      .flexibleConnectedTo(this.elementRef)
      .withPositions(PANEL_POSITIONS)
      .withFlexibleDimensions(false)
      .withPush(true);
    this.overlayRef = this.overlay.create({
      positionStrategy,
      hasBackdrop: true,
      backdropClass: "cdk-overlay-transparent-backdrop",
      minWidth: this.elementRef.nativeElement.getBoundingClientRect().width,
      scrollStrategy: this.overlay.scrollStrategies.reposition(),
    });
    this.overlayRef.attach(
      new TemplatePortal(this.panelTemplate, this.viewContainerRef),
    );
    this.overlayRef.backdropClick().subscribe(() => this.close());
    this.isOpenSignal.set(true);
  }

  close(): void {
    this.overlayRef?.dispose();
    this.overlayRef = null;
    this.isOpenSignal.set(false);
    this.onTouched();
  }

  toggleOption(option: HisHopeMultiSelectOption<T>): void {
    if (option.disabled) return;
    const current = this.valueSignal();
    const next = current.includes(option.value)
      ? current.filter((value) => value !== option.value)
      : [...current, option.value];
    this.valueSignal.set(next);
    this.onChange(next);
    this.valueChange.emit(next);
  }

  onTriggerKeydown(event: KeyboardEvent): void {
    if (this.disabled()) return;
    if (
      ["Enter", " ", "ArrowDown", "ArrowUp"].includes(event.key) &&
      !this.isOpenSignal()
    ) {
      event.preventDefault();
      this.open();
    } else if (event.key === "Escape" && this.isOpenSignal()) {
      event.preventDefault();
      this.close();
    }
  }

  onPanelKeydown(event: KeyboardEvent): void {
    const enabled = this.options().filter((option) => !option.disabled);
    if (!enabled.length) return;
    const activeIndex = enabled.findIndex(
      (option) => option.value === this.activeValue(),
    );
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      const step = event.key === "ArrowDown" ? 1 : -1;
      const next =
        enabled[(activeIndex + step + enabled.length) % enabled.length];
      this.activeValue.set(next.value);
    } else if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      const active = enabled.find(
        (option) => option.value === this.activeValue(),
      );
      if (active) this.toggleOption(active);
    } else if (event.key === "Escape") {
      event.preventDefault();
      this.close();
      this.elementRef.nativeElement.focus();
    } else if (event.key === "Home") {
      event.preventDefault();
      this.activeValue.set(enabled[0].value);
    } else if (event.key === "End") {
      event.preventDefault();
      this.activeValue.set(enabled[enabled.length - 1].value);
    }
  }

  onBlur(): void {
    if (!this.isOpenSignal()) this.onTouched();
  }

  ngOnDestroy(): void {
    this.overlayRef?.dispose();
  }
}
