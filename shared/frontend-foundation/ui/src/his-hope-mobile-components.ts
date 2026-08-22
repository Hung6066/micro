import { isPlatformBrowser } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  OnChanges,
  PLATFORM_ID,
  QueryList,
  SimpleChanges,
  ViewChildren,
  inject,
  input,
  output,
} from "@angular/core";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";

function focusableElements(host: HTMLElement): HTMLElement[] {
  return Array.from(
    host.querySelectorAll<HTMLElement>(
      'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
    ),
  );
}

@Component({
  selector: "hh-mobile-infinite-list",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div
      class="hh-mobile-infinite-list"
      role="feed"
      [attr.aria-label]="label() | hhTranslate"
    >
      <ng-content />
    </div>
    @if (loading()) {
      <div
        class="hh-mobile-infinite-list__loading"
        role="status"
        aria-live="polite"
      >
        <span
          class="hh-mobile-skeleton"
          [attr.aria-label]="'common.loading' | hhTranslate"
        ></span
        ><span class="hh-mobile-skeleton hh-mobile-skeleton--short"></span>
      </div>
    }
    @if (!loading() && hasMore()) {
      <button
        type="button"
        class="hh-mobile-button"
        (click)="requestLoadMore()"
      >
        {{ loadMoreLabel() | hhTranslate }}
      </button>
    }
    @if (!loading() && !hasMore() && loadedCount() > 0) {
      <p class="hh-mobile-list-end">{{ endLabel() | hhTranslate }}</p>
    }`,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-mobile-infinite-list {
        display: grid;
        gap: 0;
      }
      .hh-mobile-infinite-list__loading {
        display: grid;
        gap: var(--space-sm);
        padding: var(--space-lg);
      }
      .hh-mobile-skeleton {
        display: block;
        height: 12px;
        border-radius: var(--radius-pill);
        background: linear-gradient(
          90deg,
          var(--skeleton-base),
          var(--skeleton-highlight),
          var(--skeleton-base)
        );
        background-size: 200% 100%;
        animation: hh-mobile-shimmer 1.2s linear infinite;
      }
      .hh-mobile-skeleton--short {
        width: 62%;
      }
      .hh-mobile-button {
        display: block;
        width: 100%;
        min-height: var(--control-height-touch);
        margin-top: var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--color-primary);
        font: inherit;
        font-weight: var(--font-weight-semibold);
      }
      .hh-mobile-list-end {
        margin: var(--space-md) 0;
        color: var(--text-muted);
        font-size: var(--font-size-caption);
        text-align: center;
      }
      @keyframes hh-mobile-shimmer {
        to {
          background-position: -200% 0;
        }
      }
    `,
  ],
})
export class HisHopeMobileInfiniteListComponent {
  readonly label = input("common.list");
  readonly loading = input(false);
  readonly hasMore = input(false);
  readonly loadedCount = input(0);
  readonly totalCount = input<number | null>(null);
  readonly nextCursor = input("");
  readonly loadMoreLabel = input("common.loadMore");
  readonly endLabel = input("common.allRecordsLoaded");
  readonly loadMore = output<void>();
  readonly loadMoreRequested = output<{ cursor: string | null }>();
  requestLoadMore(): void {
    this.loadMore.emit();
    this.loadMoreRequested.emit({ cursor: this.nextCursor() || null });
  }
}

@Component({
  selector: "hh-mobile-refresher",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div
    class="hh-mobile-refresher"
    (pointerdown)="start($event)"
    (pointermove)="move($event)"
    (pointerup)="end()"
    (pointercancel)="end()"
  >
    <div
      class="hh-mobile-refresher__indicator"
      [style.height.px]="distance"
      [attr.aria-hidden]="distance === 0 ? 'true' : null"
    >
      <span [style.transform]="'rotate(' + (ready ? 180 : 0) + 'deg)'"
        >&#8595;</span
      >{{ (ready ? releaseLabel() : pullLabel()) | hhTranslate }}
    </div>
    <ng-content />
  </div>`,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-mobile-refresher {
        touch-action: pan-y;
      }
      .hh-mobile-refresher__indicator {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: var(--space-sm);
        overflow: hidden;
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .hh-mobile-refresher__indicator span {
        display: inline-block;
        font-size: var(--font-size-icon-sm);
        transition: transform 0.16s ease;
      }
    `,
  ],
})
export class HisHopeMobileRefresherComponent {
  readonly threshold = input(72);
  readonly pullLabel = input("common.pullToRefresh");
  readonly releaseLabel = input("common.releaseToRefresh");
  readonly refreshed = output<void>();
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  distance = 0;
  ready = false;
  private startY: number | null = null;
  start(event: PointerEvent): void {
    if (this.isBrowser && window.scrollY <= 0) this.startY = event.clientY;
  }
  move(event: PointerEvent): void {
    if (!this.isBrowser || this.startY === null || window.scrollY > 0) return;
    this.distance = Math.min(96, Math.max(0, event.clientY - this.startY));
    this.ready = this.distance >= this.threshold();
  }
  end(): void {
    const shouldRefresh = this.ready;
    this.startY = null;
    this.distance = 0;
    this.ready = false;
    if (shouldRefresh) this.refreshed.emit();
  }
}

@Component({
  selector: "hh-mobile-searchbar",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<label class="hh-mobile-searchbar"
    ><span class="material-icons" aria-hidden="true">search</span
    ><input
      #search
      type="search"
      [value]="value()"
      [placeholder]="placeholder() | hhTranslate"
      [attr.aria-label]="label() | hhTranslate"
      (input)="onInput(search.value)"
    /><button
      type="button"
      class="hh-mobile-searchbar__clear"
      [hidden]="!value()"
      [attr.aria-label]="clearLabel() | hhTranslate"
      (click)="clear(search)"
    >
      ×
    </button></label
  >`,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-mobile-searchbar {
        display: flex;
        align-items: center;
        gap: var(--space-sm);
        min-height: var(--control-height-touch);
        padding: 0 var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--text-secondary);
      }
      .hh-mobile-searchbar:focus-within {
        border-color: var(--color-primary);
        outline: var(--focus-ring-width-strong) solid
          color-mix(in srgb, var(--color-primary) 20%, transparent);
      }
      input {
        min-width: 0;
        flex: 1;
        border: 0;
        outline: 0;
        background: transparent;
        color: var(--text-primary);
        font: inherit;
      }
      .hh-mobile-searchbar__clear {
        width: var(--space-3xl);
        height: var(--space-3xl);
        border: 0;
        background: transparent;
        color: var(--text-secondary);
        font-size: var(--font-size-headline);
      }
    `,
  ],
})
export class HisHopeMobileSearchbarComponent {
  readonly value = input("");
  readonly placeholder = input("common.search");
  readonly label = input("common.search");
  readonly clearLabel = input("common.clearSearch");
  readonly valueChange = output<string>();
  private timer: ReturnType<typeof setTimeout> | null = null;
  onInput(value: string): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => this.valueChange.emit(value.trim()), 250);
  }
  clear(input: HTMLInputElement): void {
    if (this.timer) clearTimeout(this.timer);
    input.value = "";
    this.valueChange.emit("");
    input.focus();
  }
}

@Component({
  selector: "hh-mobile-action-sheet",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (open()) {
    <div class="hh-mobile-overlay" role="presentation" (click)="close.emit()">
      <section
        class="hh-mobile-sheet"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="label() | hhTranslate"
        (click)="$event.stopPropagation()"
      >
        <div class="hh-mobile-sheet__handle"></div>
        <h2>{{ label() | hhTranslate }}</h2>
        <ng-content /><button
          type="button"
          class="hh-mobile-sheet__cancel"
          (click)="close.emit()"
        >
          {{ cancelLabel() | hhTranslate }}
        </button>
      </section>
    </div>
  }`,
  styles: [
    `
      :host {
        display: contents;
      }
      .hh-mobile-overlay {
        position: fixed;
        inset: 0;
        z-index: 100;
        display: flex;
        align-items: flex-end;
        background: var(--overlay-backdrop);
      }
      .hh-mobile-sheet {
        display: grid;
        gap: var(--space-sm);
        width: 100%;
        max-height: 80dvh;
        overflow: auto;
        padding: var(--space-md) var(--space-lg)
          calc(var(--space-lg) + env(safe-area-inset-bottom));
        border-radius: var(--radius-sheet) var(--radius-sheet) 0 0;
        background: var(--surface-white);
        color: var(--text-primary);
        box-shadow: var(--shadow-sheet-up);
      }
      .hh-mobile-sheet__handle {
        width: var(--control-height-compact);
        height: 4px;
        margin: 0 auto var(--space-sm);
        border-radius: var(--radius-pill);
        background: var(--border-default);
      }
      h2 {
        margin: 0 0 var(--space-2xs);
        font-size: var(--font-size-subhead);
      }
      .hh-mobile-sheet__cancel {
        min-height: var(--control-height-touch);
        margin-top: var(--space-sm);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-white);
        font: inherit;
        font-weight: var(--font-weight-semibold);
      }
    `,
  ],
})
export class HisHopeMobileActionSheetComponent implements OnChanges {
  readonly open = input(false);
  readonly label = input("common.actions");
  readonly cancelLabel = input("common.cancel");
  readonly close = output<void>();
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private previousActiveElement: HTMLElement | null = null;
  constructor(private readonly host: ElementRef<HTMLElement>) {}
  ngOnChanges(changes: SimpleChanges): void {
    if (!this.isBrowser) return;
    if (changes["open"]?.currentValue) {
      this.previousActiveElement = document.activeElement as HTMLElement | null;
      queueMicrotask(() =>
        focusableElements(this.host.nativeElement)[0]?.focus(),
      );
    } else if (changes["open"] && !changes["open"].currentValue) {
      this.previousActiveElement?.focus();
      this.previousActiveElement = null;
    }
  }
  @HostListener("document:keydown", ["$event"]) onKeydown(
    event: KeyboardEvent,
  ): void {
    if (!this.isBrowser || !this.open()) return;
    if (event.key === "Escape") {
      event.preventDefault();
      this.close.emit();
      return;
    }
    if (event.key !== "Tab") return;
    const items = focusableElements(this.host.nativeElement);
    if (!items.length) return;
    const first = items[0];
    const last = items[items.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }
}

@Component({
  selector: "hh-mobile-bottom-sheet",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (open()) {
    <div class="hh-mobile-overlay" role="presentation" (click)="close.emit()">
      <section
        class="hh-mobile-bottom-sheet"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="label() | hhTranslate"
        (click)="$event.stopPropagation()"
      >
        <header>
          <h2>{{ label() | hhTranslate }}</h2>
          <button
            type="button"
            class="hh-mobile-icon-button"
            [attr.aria-label]="closeLabel() | hhTranslate"
            (click)="close.emit()"
          >
            ×
          </button>
        </header>
        <div class="hh-mobile-bottom-sheet__content"><ng-content /></div>
      </section>
    </div>
  }`,
  styles: [
    `
      :host {
        display: contents;
      }
      .hh-mobile-overlay {
        position: fixed;
        inset: 0;
        z-index: 100;
        display: flex;
        align-items: flex-end;
        background: var(--overlay-backdrop);
      }
      .hh-mobile-bottom-sheet {
        width: 100%;
        max-height: 90dvh;
        overflow: auto;
        border-radius: var(--radius-sheet) var(--radius-sheet) 0 0;
        background: var(--surface-white);
        box-shadow: var(--shadow-sheet-up);
      }
      header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-md);
        padding: var(--space-lg);
        border-bottom: 1px solid var(--border-light);
      }
      h2 {
        margin: 0;
        font-size: var(--font-size-subhead);
      }
      .hh-mobile-icon-button {
        width: var(--control-height);
        height: var(--control-height);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-full);
        background: transparent;
        font-size: var(--font-size-title);
      }
      .hh-mobile-bottom-sheet__content {
        padding: var(--space-lg) calc(var(--space-lg) + env(safe-area-inset-right))
          calc(var(--space-xl) + env(safe-area-inset-bottom))
          calc(var(--space-lg) + env(safe-area-inset-left));
      }
    `,
  ],
})
export class HisHopeMobileBottomSheetComponent implements OnChanges {
  readonly open = input(false);
  readonly label = input("common.details");
  readonly closeLabel = input("common.close");
  readonly close = output<void>();
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private previousActiveElement: HTMLElement | null = null;
  constructor(private readonly host: ElementRef<HTMLElement>) {}
  ngOnChanges(changes: SimpleChanges): void {
    if (!this.isBrowser) return;
    if (changes["open"]?.currentValue) {
      this.previousActiveElement = document.activeElement as HTMLElement | null;
      queueMicrotask(() =>
        focusableElements(this.host.nativeElement)[0]?.focus(),
      );
    } else if (changes["open"] && !changes["open"].currentValue) {
      this.previousActiveElement?.focus();
      this.previousActiveElement = null;
    }
  }
  @HostListener("document:keydown", ["$event"]) onKeydown(
    event: KeyboardEvent,
  ): void {
    if (!this.isBrowser || !this.open()) return;
    if (event.key === "Escape") {
      event.preventDefault();
      this.close.emit();
      return;
    }
    if (event.key !== "Tab") return;
    const items = focusableElements(this.host.nativeElement);
    if (!items.length) return;
    const first = items[0];
    const last = items[items.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }
}

@Component({
  selector: "hh-mobile-segment",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div
    class="hh-mobile-segment"
    role="tablist"
    [attr.aria-label]="label() | hhTranslate"
  >
    @for (option of options(); track option.value) {
      <button
        type="button"
        role="tab"
        [attr.aria-selected]="option.value === value()"
        [class.is-active]="option.value === value()"
        (click)="valueChange.emit(option.value)"
      >
        {{ option.label | hhTranslate }}
      </button>
    }
  </div>`,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-mobile-segment {
        display: grid;
        grid-auto-flow: column;
        grid-auto-columns: 1fr;
        gap: var(--space-2xs);
        padding: var(--space-2xs);
        border-radius: var(--radius-control);
        background: var(--surface-subtle);
      }
      button {
        min-height: var(--control-height);
        border: 0;
        border-radius: var(--radius-control);
        background: transparent;
        color: var(--text-secondary);
        font: inherit;
      }
      button.is-active {
        background: var(--surface-white);
        color: var(--color-primary);
        font-weight: var(--font-weight-semibold);
        box-shadow: var(--shadow-control);
      }
    `,
  ],
})
export class HisHopeMobileSegmentComponent {
  readonly label = input("common.view");
  readonly options = input<Array<{ value: string; label: string }>>([]);
  readonly value = input("");
  readonly valueChange = output<string>();
}

@Component({
  selector: "hh-mobile-accordion",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="hh-mobile-accordion">
    <button type="button" [attr.aria-expanded]="open" (click)="open = !open">
      <span>{{ title() | hhTranslate }}</span
      ><span aria-hidden="true">{{ open ? "−" : "+" }}</span>
    </button>
    @if (open) {
      <div class="hh-mobile-accordion__content"><ng-content /></div>
    }
  </section>`,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-mobile-accordion {
        border-bottom: 1px solid var(--border-light);
      }
      button {
        display: flex;
        align-items: center;
        justify-content: space-between;
        width: 100%;
        min-height: 52px;
        padding: var(--space-md) 0;
        border: 0;
        background: transparent;
        color: var(--text-primary);
        font: inherit;
        font-weight: var(--font-weight-semibold);
        text-align: left;
      }
      .hh-mobile-accordion__content {
        padding: 0 0 var(--space-lg);
        color: var(--text-secondary);
        line-height: 1.5;
      }
    `,
  ],
})
export class HisHopeMobileAccordionComponent {
  readonly title = input("common.section");
  open = false;
}

@Component({
  selector: "hh-mobile-avatar",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span
    class="hh-mobile-avatar"
    [class.hh-mobile-avatar--image]="src()"
    [attr.aria-label]="label() | hhTranslate"
  >
    @if (src()) {
      <img [src]="src()" [alt]="label() | hhTranslate" />
    } @else {
      {{ initials() }}
    }
  </span>`,
  styles: [
    `
      :host {
        display: inline-flex;
      }
      .hh-mobile-avatar {
        display: grid;
        place-items: center;
        width: var(--button-height);
        height: var(--button-height);
        overflow: hidden;
        border-radius: var(--radius-full);
        background: var(--color-primary-soft);
        color: var(--color-primary);
        font-weight: var(--font-weight-semibold);
      }
      img {
        width: 100%;
        height: 100%;
        object-fit: cover;
      }
    `,
  ],
})
export class HisHopeMobileAvatarComponent {
  readonly label = input("common.avatar");
  readonly src = input("");
  readonly initials = input("");
}

@Component({
  selector: "hh-mobile-date-time",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<label class="hh-mobile-field"
    ><span>{{ label() | hhTranslate }}</span
    ><input
      [type]="mode()"
      [value]="value()"
      [min]="min() || null"
      [max]="max() || null"
      (input)="valueChange.emit($any($event.target).value)"
  /></label>`,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-mobile-field {
        display: grid;
        gap: var(--space-xs);
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
        font-weight: var(--font-weight-semibold);
      }
      input {
        box-sizing: border-box;
        width: 100%;
        min-height: var(--control-height-touch);
        padding: 0 var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
        font-size: var(--font-size-input);
      }
    `,
  ],
})
export class HisHopeMobileDateTimeComponent {
  readonly label = input("common.dateAndTime");
  readonly mode = input<"date" | "datetime-local">("datetime-local");
  readonly value = input("");
  readonly min = input("");
  readonly max = input("");
  readonly valueChange = output<string>();
}

@Component({
  selector: "hh-mobile-otp",
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div
    class="hh-mobile-otp"
    role="group"
    [attr.aria-label]="label() | hhTranslate"
  >
    @for (digit of digits; track $index) {
      <input
        #otpInput
        inputmode="numeric"
        autocomplete="one-time-code"
        maxlength="1"
        [value]="digit"
        [attr.aria-label]="(label() | hhTranslate) + ' ' + ($index + 1)"
        (input)="update($index, $any($event.target).value)"
        (keydown.backspace)="backspace($index)"
        (paste)="paste($event)"
      />
    }
  </div>`,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-mobile-otp {
        display: flex;
        justify-content: center;
        gap: var(--space-sm);
      }
      .hh-mobile-otp input {
        width: var(--touch-target);
        height: 52px;
        box-sizing: border-box;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--text-primary);
        font: inherit;
        font-size: var(--font-size-headline);
        text-align: center;
      }
      .hh-mobile-otp input:focus {
        border-color: var(--color-primary);
        outline: var(--focus-ring-width-strong) solid
          color-mix(in srgb, var(--color-primary) 20%, transparent);
      }
    `,
  ],
})
export class HisHopeMobileOtpComponent {
  readonly label = input("common.verificationCode");
  readonly length = input(6);
  readonly valueChange = output<string>();
  readonly completed = output<string>();
  digits: string[] = [];
  @ViewChildren("otpInput") private inputs!: QueryList<
    ElementRef<HTMLInputElement>
  >;
  constructor() {
    this.digits = Array.from({ length: this.length() }, () => "");
  }
  update(index: number, value: string): void {
    const digit = value.replace(/\D/g, "").slice(-1);
    this.digits[index] = digit;
    this.valueChange.emit(this.digits.join(""));
    if (digit && index < this.digits.length - 1)
      this.inputs?.get(index + 1)?.nativeElement.focus();
    if (this.digits.every(Boolean)) this.completed.emit(this.digits.join(""));
  }
  backspace(index: number): void {
    if (!this.digits[index] && index > 0)
      this.inputs?.get(index - 1)?.nativeElement.focus();
  }
  paste(event: ClipboardEvent): void {
    event.preventDefault();
    const value =
      event.clipboardData
        ?.getData("text")
        ?.replace(/\D/g, "")
        .slice(0, this.digits.length) ?? "";
    [...value].forEach((digit, index) => (this.digits[index] = digit));
    this.valueChange.emit(this.digits.join(""));
    if (this.digits.every(Boolean)) this.completed.emit(this.digits.join(""));
  }
}
