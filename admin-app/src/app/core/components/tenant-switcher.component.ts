import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  QueryList,
  ViewChildren,
  input,
  output,
  signal,
} from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import { TenantOption } from "../services/tenant-context.service";

@Component({
  selector: "app-tenant-switcher",
  standalone: true,
  imports: [MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="hh-tenant-switcher"
      (mouseenter)="open.set(true)"
      (mouseleave)="closeOnPointerLeave()"
    >
      <button
        #trigger
        type="button"
        class="hh-tenant-trigger"
        [attr.aria-expanded]="open()"
        aria-haspopup="listbox"
        [attr.aria-controls]="menuId"
        [attr.aria-label]="triggerLabel()"
        [attr.title]="triggerLabel()"
        (click)="toggle()"
        (keydown)="onTriggerKeydown($event)"
      >
        <span class="hh-tenant-icon" aria-hidden="true">{{
          shortCode(activeKey(), tenants())
        }}</span>
        <span class="hh-tenant-label">{{ activeLabel() }}</span>
        <mat-icon class="hh-tenant-chevron" aria-hidden="true"
          >expand_more</mat-icon
        >
      </button>
      @if (open()) {
        <div
          [id]="menuId"
          class="hh-tenant-menu"
          role="listbox"
          aria-label="Tenant options"
          (keydown)="onMenuKeydown($event)"
        >
          @for (tenant of tenants(); track tenant.key) {
            <button
              #option
              type="button"
              role="option"
              class="hh-tenant-option"
              [attr.aria-selected]="activeKey() === tenant.key"
              [tabIndex]="activeKey() === tenant.key ? 0 : -1"
              (click)="select(tenant.key)"
            >
              <span class="hh-tenant-icon" aria-hidden="true">{{
                shortCode(tenant.key)
              }}</span>
              <span class="hh-tenant-option-label">{{ tenant.label }}</span>
            </button>
          }
        </div>
      }
    </div>
  `,
  styles: [
    `
      :host {
        display: inline-block;
        z-index: 30;
      }

      .hh-tenant-switcher {
        position: relative;
        display: inline-block;
        padding-bottom: var(--space-xs);
        color: inherit;
      }

      .hh-tenant-trigger,
      .hh-tenant-option {
        display: inline-flex;
        align-items: center;
        gap: var(--space-sm);
        min-height: var(--touch-target);
        border: 1px solid currentColor;
        border-radius: var(--radius-button);
        padding: 0 var(--space-md);
        background: transparent;
        color: inherit;
        font: inherit;
        cursor: pointer;
        box-sizing: border-box;
      }

      .hh-tenant-trigger {
        max-width: 13rem;
        justify-content: flex-start;
      }

      .hh-tenant-trigger:focus-visible,
      .hh-tenant-option:focus-visible {
        outline: var(--focus-ring-width) solid var(--color-focus);
        outline-offset: var(--focus-ring-width);
      }

      .hh-tenant-option {
        width: 100%;
        min-height: var(--menu-item-height);
        justify-content: flex-start;
        border: 0;
        border-radius: var(--radius-button);
        padding: var(--space-sm) var(--space-md);
        text-align: start;
      }

      .hh-tenant-option:hover,
      .hh-tenant-option:focus-visible {
        background: var(--surface-hover);
        outline: 0;
      }

      .hh-tenant-option[aria-selected="true"] {
        background: var(--color-primary-soft);
        color: var(--color-primary-deep);
      }

      .hh-tenant-icon {
        display: inline-grid;
        place-items: center;
        width: var(--size-nav-icon-track);
        height: var(--size-nav-icon-track);
        flex: 0 0 var(--size-nav-icon-track);
        border: 1px solid var(--border-default);
        border-radius: 50%;
        background: var(--surface-white);
        color: var(--color-primary-deep);
        font-size: var(--font-size-caption);
        font-weight: var(--font-weight-semibold);
        letter-spacing: 0.04em;
        line-height: 1;
      }

      .hh-tenant-label {
        flex: 1;
        min-width: 0;
        overflow: hidden;
        font-size: var(--font-size-label);
        font-weight: var(--font-weight-semibold);
        letter-spacing: 0.01em;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .hh-tenant-chevron {
        width: 1.125rem;
        height: 1.125rem;
        flex: 0 0 1.125rem;
        font-size: 1.125rem;
        opacity: 0.88;
      }

      .hh-tenant-option-label {
        flex: 1;
        min-width: 0;
        color: var(--text-secondary);
        font-size: var(--font-size-body);
        font-weight: var(--font-weight-regular);
        text-align: start;
        white-space: nowrap;
      }

      .hh-tenant-option[aria-selected="true"] .hh-tenant-option-label {
        color: inherit;
        font-weight: var(--font-weight-semibold);
      }

      .hh-tenant-menu {
        position: absolute;
        top: calc(100% - 1px);
        inset-inline-end: 0;
        z-index: 20;
        display: flex;
        flex-direction: column;
        align-items: stretch;
        gap: var(--menu-item-gap);
        box-sizing: border-box;
        width: min(16rem, calc(100vw - var(--space-3xl)));
        max-height: min(18rem, 70vh);
        padding: var(--menu-item-gap);
        overflow-y: auto;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        box-shadow: var(--shadow-dropdown);
      }

      @media (max-width: 640px) {
        .hh-tenant-menu {
          position: fixed;
          top: var(--shell-header-height);
          right: var(--size-timeline-rail);
          bottom: auto;
          left: auto;
          width: min(16rem, calc(100vw - var(--space-3xl)));
          max-height: calc(
            100vh - var(--shell-header-height) - var(--space-md)
          );
        }
      }
    `,
  ],
})
export class TenantSwitcherComponent {
  readonly tenants = input<TenantOption[]>([]);
  readonly activeKey = input<string | null>(null);
  readonly tenantChange = output<string>();

  readonly open = signal(false);
  readonly menuId = `hh-tenant-menu-${Math.random().toString(36).slice(2, 9)}`;

  @ViewChildren("option")
  private readonly optionButtons!: QueryList<ElementRef<HTMLButtonElement>>;
  @ViewChildren("trigger")
  private readonly triggerButtons!: QueryList<ElementRef<HTMLButtonElement>>;

  activeLabel(): string {
    const key = this.activeKey();
    if (!key) return "Tenant";
    return (
      this.tenants().find((tenant) => tenant.key === key)?.label ?? key
    );
  }

  triggerLabel(): string {
    return `Tenant: ${this.activeLabel()}`;
  }

  shortCode(key: string | null, tenants: TenantOption[] = []): string {
    const resolved = key ?? tenants[0]?.key ?? "TN";
    return resolved
      .split("-")
      .map((part) => part.charAt(0))
      .join("")
      .slice(0, 2)
      .toUpperCase();
  }

  select(tenantKey: string): void {
    this.tenantChange.emit(tenantKey);
    this.open.set(false);
    this.focusTrigger();
  }

  toggle(): void {
    this.open.update((value) => !value);
    if (!this.open()) return;
    queueMicrotask(() => this.focusSelectedOption());
  }

  closeOnPointerLeave(): void {
    this.open.set(false);
  }

  onTriggerKeydown(event: KeyboardEvent): void {
    if (
      event.key === "ArrowDown" ||
      event.key === "Enter" ||
      event.key === " "
    ) {
      event.preventDefault();
      if (!this.open()) this.open.set(true);
      queueMicrotask(() => this.focusSelectedOption());
    }
  }

  onMenuKeydown(event: KeyboardEvent): void {
    const buttons = this.optionButtons?.toArray() ?? [];
    if (!buttons.length) return;

    const current = document.activeElement as HTMLButtonElement;
    const currentIndex = buttons.findIndex(
      (button) => button.nativeElement === current,
    );
    let nextIndex = currentIndex;

    if (event.key === "ArrowDown") {
      nextIndex = (currentIndex + 1) % buttons.length;
    } else if (event.key === "ArrowUp") {
      nextIndex = (currentIndex - 1 + buttons.length) % buttons.length;
    } else if (event.key === "Home") {
      nextIndex = 0;
    } else if (event.key === "End") {
      nextIndex = buttons.length - 1;
    } else if (event.key === "Escape") {
      event.preventDefault();
      this.open.set(false);
      this.focusTrigger();
      return;
    } else if (event.key === "Tab") {
      this.open.set(false);
      return;
    } else {
      return;
    }

    event.preventDefault();
    buttons[nextIndex].nativeElement.focus();
  }

  @HostListener("document:keydown.escape")
  closeOnEscape(): void {
    if (!this.open()) return;
    this.open.set(false);
    this.focusTrigger();
  }

  private focusSelectedOption(): void {
    const buttons = this.optionButtons?.toArray() ?? [];
    const index = this.tenants().findIndex(
      (tenant) => tenant.key === this.activeKey(),
    );
    buttons[index < 0 ? 0 : index]?.nativeElement.focus();
  }

  private focusTrigger(): void {
    this.triggerButtons?.first?.nativeElement.focus();
  }
}
