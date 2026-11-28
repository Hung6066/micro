import { ConnectedPosition, Overlay, OverlayRef } from "@angular/cdk/overlay";
import { TemplatePortal } from "@angular/cdk/portal";
import { CommonModule } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  AfterViewInit,
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
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

export interface HisHopeSelectOption<T = string> {
  value: T;
  label: string;
  icon?: string;
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
 * Accessible single-select combobox with a `role="listbox"` overlay panel,
 * keyboard navigation and typeahead. Implements `ControlValueAccessor` so it
 * binds with `formControlName`/`[(ngModel)]` like a native `<select>`.
 * Compose inside `hh-form-field` for label/hint/error semantics.
 */
@Component({
  selector: "hh-select",
  standalone: true,
  imports: [CommonModule, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => HisHopeSelectComponent),
      multi: true,
    },
  ],
  host: {
    class: "hh-select",
    "[class.hh-select--compact]": "appearance() === 'compact'",
    role: "combobox",
    "[attr.aria-expanded]": "isOpen()",
    "[attr.aria-disabled]": "disabled()",
    "[attr.aria-label]": "labelText()",
    "[attr.tabindex]": "disabled() ? -1 : 0",
    "(click)": "toggle()",
    "(keydown)": "onTriggerKeydown($event)",
    "(blur)": "onBlur()",
  },
  template: `
    @if (selectedOption()?.icon) {
      <span class="hh-select__icon material-icons" aria-hidden="true">{{ selectedOption()?.icon }}</span>
    }
    <span
      class="hh-select__value"
      [class.hh-select__value--placeholder]="!selectedOption()"
    >
      {{ selectedOption()?.label ?? (placeholder() | hhTranslate) }}
    </span>
    <span class="hh-select__caret material-icons" aria-hidden="true"
      >expand_more</span
    >
    <span class="hh-select__projected-options" aria-hidden="true">
      <ng-content select="option"></ng-content>
    </span>
    <ng-template #panelTemplate>
      <ul
        #panel
        class="hh-select__panel"
        role="listbox"
        tabindex="-1"
        [attr.aria-label]="labelText()"
        (keydown)="onPanelKeydown($event)"
      >
        @for (option of availableOptions(); track option.value) {
          <li
            role="option"
            class="hh-select__option"
            [class.hh-select__option--active]="option.value === activeValue()"
            [class.hh-select__option--selected]="option.value === value()"
            [attr.aria-selected]="option.value === value()"
            [attr.aria-disabled]="option.disabled"
            (click)="selectOption(option)"
            (mouseenter)="activeValue.set(option.value)"
          >
            @if (option.icon) {
              <span class="hh-select__icon material-icons" aria-hidden="true">{{ option.icon }}</span>
            }
            {{ option.label }}
          </li>
        } @empty {
          <li class="hh-select__empty" role="presentation">
            {{ emptyLabel() | hhTranslate }}
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
        gap: var(--space-sm);
        min-height: var(--control-height);
        box-sizing: border-box;
        width: 100%;
        min-width: 0;
        padding: 0 var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
        cursor: pointer;
      }
      .hh-select__projected-options {
        display: none !important;
      }
      :host(.hh-select--compact) {
        width: auto;
        min-width: 12rem;
        min-height: var(--touch-target);
        border-radius: var(--radius-button);
        padding-inline: var(--space-sm) var(--space-md);
      }
      .hh-select__icon {
        flex: 0 0 auto;
        color: currentColor;
        font-size: var(--font-size-icon-sm);
      }
      :host(:focus-visible) {
        border-color: var(--color-primary);
        outline: var(--focus-ring-width-strong) solid
          color-mix(in srgb, var(--color-primary) 20%, transparent);
      }
      :host([aria-disabled="true"]) {
        cursor: not-allowed;
        opacity: 0.6;
      }
      .hh-select__value--placeholder {
        color: var(--text-muted);
      }
      .hh-select__value {
        min-width: 0;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .hh-select__caret {
        color: var(--text-secondary);
        font-size: var(--font-size-section);
      }
      .hh-select__panel {
        margin: 0;
        width: 100%;
        box-sizing: border-box;
        min-width: 180px;
        max-height: min(320px, 60vh);
        overflow: auto;
        padding: var(--space-2xs);
        list-style: none;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        box-shadow: var(--shadow-dropdown);
      }
      .hh-select__option {
        display: flex;
        align-items: center;
        gap: var(--space-sm);
        padding: var(--space-sm) var(--space-md);
        border-radius: var(--radius-card);
        cursor: pointer;
      }
      .hh-select__option--active {
        background: var(--surface-hover);
      }
      .hh-select__option--selected {
        font-weight: var(--font-weight-semibold);
        color: var(--color-primary);
      }
      .hh-select__option[aria-disabled="true"] {
        color: var(--text-muted);
        cursor: not-allowed;
      }
      .hh-select__empty {
        padding: var(--space-md);
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
        text-align: center;
      }
    `,
  ],
})
export class HisHopeSelectComponent<T = string>
  implements ControlValueAccessor, OnDestroy, AfterViewInit
{
  private readonly i18n = inject(HisHopeI18nService);
  readonly options = input<HisHopeSelectOption<T>[]>([]);
  readonly label = input("common.select");
  readonly placeholder = input("common.selectAnOption");
  readonly emptyLabel = input("common.noOptionsAvailable");
  readonly appearance = input<"field" | "compact">("field");
  readonly labelText = computed(() => this.i18n.t(this.label(), this.label()));
  readonly valueChange = output<T | null>();

  @ViewChild("panelTemplate")
  private readonly panelTemplate!: TemplateRef<unknown>;

  private readonly overlay = inject(Overlay);
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private readonly viewContainerRef = inject(ViewContainerRef);
  private overlayRef: OverlayRef | null = null;

  private readonly valueSignal = signal<T | null>(null);
  readonly value = this.valueSignal.asReadonly();
  readonly activeValue = signal<T | null>(null);
  private readonly isOpenSignal = signal(false);
  readonly isOpen = this.isOpenSignal.asReadonly();
  readonly disabled = signal(false);
  readonly selectedOption = computed(() =>
    this.availableOptions().find((option) => option.value === this.valueSignal()),
  );
  private readonly projectedOptions = signal<HisHopeSelectOption<string>[]>([]);
  readonly availableOptions = computed<HisHopeSelectOption<T>[]>(() =>
    this.options().length
      ? this.options()
      : (this.projectedOptions() as unknown as HisHopeSelectOption<T>[]),
  );

  private onChange: (value: T | null) => void = () => {};
  private onTouched: () => void = () => {};
  private projectedOptionsObserver: MutationObserver | null = null;

  ngAfterViewInit(): void {
    this.syncProjectedOptions();
    this.projectedOptionsObserver = new MutationObserver(() => this.syncProjectedOptions());
    this.projectedOptionsObserver.observe(this.elementRef.nativeElement, {
      subtree: true,
      childList: true,
      attributes: true,
      attributeFilter: ["value", "disabled"],
    });
  }

  writeValue(value: T | null): void {
    this.valueSignal.set(value);
  }

  registerOnChange(fn: (value: T | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  toggle(): void {
    if (this.disabled()) return;
    this.isOpenSignal() ? this.close() : this.open();
  }

  open(): void {
    if (this.overlayRef || this.disabled()) return;
    this.activeValue.set(this.value() ?? this.availableOptions()[0]?.value ?? null);
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
      width: `${this.elementRef.nativeElement.getBoundingClientRect().width}px`,
      minWidth: this.elementRef.nativeElement.getBoundingClientRect().width,
      scrollStrategy: this.overlay.scrollStrategies.reposition(),
    });
    const triggerStyle = getComputedStyle(this.elementRef.nativeElement);
    this.overlayRef.overlayElement.style.fontFamily = triggerStyle.fontFamily;
    this.overlayRef.overlayElement.style.fontSize = triggerStyle.fontSize;
    this.overlayRef.overlayElement.style.fontWeight = triggerStyle.fontWeight;
    this.overlayRef.overlayElement.style.lineHeight = triggerStyle.lineHeight;
    this.overlayRef.attach(
      new TemplatePortal(this.panelTemplate, this.viewContainerRef),
    );
    queueMicrotask(() =>
      (this.overlayRef?.overlayElement.querySelector(".hh-select__panel") as HTMLElement | null)?.focus(),
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

  selectOption(option: HisHopeSelectOption<T>): void {
    if (option.disabled) return;
    this.valueSignal.set(option.value);
    this.onChange(option.value);
    this.valueChange.emit(option.value);
    this.close();
    this.elementRef.nativeElement.focus();
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
    const enabled = this.availableOptions().filter((option) => !option.disabled);
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
      if (active) this.selectOption(active);
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
    this.projectedOptionsObserver?.disconnect();
  }

  private syncProjectedOptions(): void {
    if (this.options().length) return;
    const projected = Array.from(
      this.elementRef.nativeElement.querySelectorAll("option") as NodeListOf<HTMLOptionElement>,
    ).map((option) => ({
      value: option.value,
      label: option.textContent?.trim() ?? "",
      disabled: option.disabled,
    }));
    this.projectedOptions.set(projected);
  }
}
