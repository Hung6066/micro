import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Output,
  ViewChild,
  effect,
  inject,
  input,
} from "@angular/core";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";

@Component({
  selector: "hh-drawer",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (open()) {
      <div class="hh-drawer-backdrop" (click)="close()"></div>
    }
    <aside
      #panel
      class="hh-drawer"
      [class.hh-drawer--open]="open()"
      [attr.aria-hidden]="!open()"
      [attr.aria-label]="label()"
      role="dialog"
      aria-modal="true"
    >
      <button
        type="button"
        class="hh-drawer__close"
        (click)="close()"
        aria-label="Close"
      >
        <span class="material-icons" aria-hidden="true">close</span></button
      ><ng-content />
    </aside>`,
  styles: [
    `
      :host {
        display: contents;
      }
      .hh-drawer-backdrop {
        position: fixed;
        inset: 0;
        z-index: 1190;
        background: rgba(15, 35, 25, 0.28);
      }
      .hh-drawer {
        position: fixed;
        inset: 0 0 0 auto;
        z-index: 1200;
        width: min(420px, 92vw);
        padding: 24px;
        transform: translateX(100%);
        visibility: hidden;
        overflow: auto;
        background: var(--surface-white);
        box-shadow: -12px 0 32px rgba(15, 35, 25, 0.16);
        transition:
          transform var(--motion-base) var(--ease-standard),
          visibility var(--motion-base);
      }
      .hh-drawer--open {
        transform: translateX(0);
        visibility: visible;
      }
      .hh-drawer__close {
        float: right;
        display: grid;
        place-items: center;
        width: var(--touch-target);
        height: var(--touch-target);
        border: 0;
        border-radius: var(--radius-button);
        background: transparent;
        color: var(--text-secondary);
        cursor: pointer;
      }
    `,
  ],
})
export class HisHopeDrawerComponent {
  readonly open = input(false);
  readonly label = input("Drawer");
  @Output() readonly closed = new EventEmitter<void>();
  @ViewChild("panel") private panel?: ElementRef<HTMLElement>;
  private previousFocus: HTMLElement | null = null;

  constructor() {
    effect(() => {
      if (this.open()) {
        this.previousFocus =
          typeof document === "undefined"
            ? null
            : (document.activeElement as HTMLElement);
        setTimeout(() =>
          this.panel?.nativeElement
            .querySelector<HTMLElement>(
              'button,input,select,textarea,a,[tabindex]:not([tabindex="-1"])',
            )
            ?.focus(),
        );
      }
    });
  }
  close(): void {
    this.closed.emit();
    setTimeout(() => this.previousFocus?.focus());
  }
  @HostListener("document:keydown.escape") onEscape(): void {
    if (this.open()) this.close();
  }
  @HostListener("document:keydown.tab", ["$event"]) onTab(event: Event): void {
    const keyboardEvent = event as KeyboardEvent;
    if (!this.open() || !this.panel) return;
    const focusables = Array.from(
      this.panel.nativeElement.querySelectorAll<HTMLElement>(
        'button,input,select,textarea,a,[tabindex]:not([tabindex="-1"])',
      ),
    );
    if (!focusables.length) return;
    const first = focusables[0];
    const last = focusables[focusables.length - 1];
    if (keyboardEvent.shiftKey && document.activeElement === first) {
      keyboardEvent.preventDefault();
      last.focus();
    } else if (!keyboardEvent.shiftKey && document.activeElement === last) {
      keyboardEvent.preventDefault();
      first.focus();
    }
  }
}

@Component({
  selector: "hh-popover",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<details #details class="hh-popover" (toggle)="onToggle()">
    <summary><ng-content select="[hhPopoverTrigger]" /></summary>
    <div class="hh-popover__panel" role="dialog" (keydown)="onKeydown($event)">
      <ng-content />
    </div>
  </details>`,
  styles: [
    `
      :host {
        display: inline-block;
        position: relative;
        min-width: 0;
      }
      .hh-popover {
        position: relative;
      }
      .hh-popover summary {
        list-style: none;
        cursor: pointer;
      }
      .hh-popover summary::-webkit-details-marker {
        display: none;
      }
      .hh-popover__panel {
        position: absolute;
        top: calc(100% + 8px);
        right: 0;
        z-index: 10;
        width: min(320px, calc(100vw - 32px));
        min-width: 0;
        max-height: min(70vh, 480px);
        overflow: auto;
        padding: 12px;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        box-shadow: var(--shadow-dropdown);
      }
      @media (max-width: 640px) {
        .hh-popover__panel {
          position: fixed;
          top: auto;
          right: 16px;
          bottom: 16px;
          left: 16px;
          width: auto;
          max-height: 70vh;
        }
      }
    `,
  ],
})
export class HisHopePopoverComponent {
  @ViewChild("details") private details?: ElementRef<HTMLDetailsElement>;
  private previousFocus: HTMLElement | null = null;
  onToggle(): void {
    if (!this.details) return;
    if (this.details.nativeElement.open) {
      this.previousFocus =
        typeof document === "undefined"
          ? null
          : (document.activeElement as HTMLElement);
      setTimeout(() =>
        this.details?.nativeElement
          .querySelector<HTMLElement>(
            '.hh-popover__panel button,.hh-popover__panel a,[tabindex]:not([tabindex="-1"])',
          )
          ?.focus(),
      );
    } else {
      setTimeout(() => this.previousFocus?.focus());
    }
  }
  onKeydown(event: KeyboardEvent): void {
    if (event.key !== "Tab" || !this.details?.nativeElement.open) return;
    const focusables = Array.from(
      this.details.nativeElement.querySelectorAll<HTMLElement>(
        '.hh-popover__panel button,.hh-popover__panel a,.hh-popover__panel input,.hh-popover__panel select,.hh-popover__panel textarea,[tabindex]:not([tabindex="-1"])',
      ),
    );
    if (!focusables.length) return;
    const first = focusables[0];
    const last = focusables[focusables.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }
  @HostListener("document:keydown.escape") onEscape(): void {
    if (this.details?.nativeElement.open) {
      this.details.nativeElement.open = false;
      this.onToggle();
    }
  }
}

@Component({
  selector: "hh-tabs",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div
    class="hh-tabs"
    role="tablist"
    [attr.aria-label]="label()"
    (keydown)="onKeydown($event)"
  >
    <ng-content />
  </div>`,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-tabs {
        display: flex;
        gap: 4px;
        overflow-x: auto;
        border-bottom: 1px solid var(--border-default);
      }
    `,
  ],
})
export class HisHopeTabsComponent {
  readonly label = input("Tabs");
  onKeydown(event: KeyboardEvent): void {
    const tabs = Array.from(
      (event.currentTarget as HTMLElement).querySelectorAll<HTMLElement>(
        '[role="tab"]',
      ),
    );
    const index = tabs.indexOf(document.activeElement as HTMLElement);
    if (
      index < 0 ||
      !["ArrowRight", "ArrowLeft", "Home", "End"].includes(event.key)
    )
      return;
    event.preventDefault();
    const next =
      event.key === "Home"
        ? 0
        : event.key === "End"
          ? tabs.length - 1
          : (index + (event.key === "ArrowRight" ? 1 : -1) + tabs.length) %
            tabs.length;
    tabs[next]?.focus();
  }
}

@Component({
  selector: "hh-breadcrumb",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<nav [attr.aria-label]="label()">
    <ol class="hh-breadcrumb">
      <ng-content />
    </ol>
  </nav>`,
  styles: [
    `
      :host {
        display: block;
      }
      .hh-breadcrumb {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
        margin: 0;
        padding: 0;
        list-style: none;
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
      .hh-breadcrumb ::ng-deep li + li::before {
        content: "/";
        margin-right: 8px;
        color: var(--text-muted);
      }
    `,
  ],
})
export class HisHopeBreadcrumbComponent {
  readonly label = input("Breadcrumb");
}

@Component({
  selector: "hh-permission-button",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (canRender() || showDenied()) {
    <button
      type="button"
      [disabled]="disabled() || !canRender()"
      [attr.aria-disabled]="disabled() || !canRender()"
      [attr.aria-describedby]="!canRender() ? deniedId : null"
      (click)="clicked.emit()"
    >
      <ng-content /></button
    ><span [id]="deniedId" class="hh-permission-denied">{{
      deniedLabel()
    }}</span>
  }`,
  styles: [
    `
      :host {
        display: inline-block;
      }
      button {
        min-height: var(--control-height);
        padding: 0 14px;
        border: 1px solid var(--button-secondary-border);
        border-radius: var(--radius-button);
        background: var(--button-secondary-bg);
        color: var(--button-secondary-text);
        font: inherit;
        font-weight: var(--button-font-weight);
        cursor: pointer;
      }
      button:disabled {
        cursor: not-allowed;
        opacity: 0.55;
      }
      .hh-permission-denied {
        position: absolute;
        width: 1px;
        height: 1px;
        overflow: hidden;
        clip: rect(0 0 0 0);
      }
    `,
  ],
})
export class HisHopePermissionButtonComponent {
  private readonly permissionService = inject(HisHopePermissionService);
  readonly deniedId = `hh-permission-denied-${Math.random().toString(36).slice(2)}`;
  readonly requiredPermission = input("");
  readonly requiredPermissions = input<string[]>([]);
  readonly permissionMode = input<"any" | "all">("all");
  readonly permissions = input<string[]>([]);
  readonly disabled = input(false);
  readonly showDenied = input(false);
  readonly deniedLabel = input(
    "You do not have permission to perform this action.",
  );
  @Output() readonly clicked = new EventEmitter<void>();
  canRender(): boolean {
    const required = this.requiredPermissions().length
      ? this.requiredPermissions()
      : this.requiredPermission()
        ? [this.requiredPermission()]
        : [];
    if (!required.length) return true;
    const localPermissions = this.permissions();
    if (localPermissions.length) {
      return this.permissionMode() === "any"
        ? required.some((permission) => localPermissions.includes(permission))
        : required.every((permission) => localPermissions.includes(permission));
    }
    return this.permissionMode() === "any"
      ? this.permissionService.hasAny(required)
      : this.permissionService.hasAll(required);
  }
}
