import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  QueryList,
  ViewChildren,
  inject,
  input,
  output,
  signal,
} from "@angular/core";
import { MatIconModule } from "@angular/material/icon";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";

export interface WorkspaceSwitcherOption {
  id: string;
  labelKey: string;
  fallback: string;
  icon: string;
}

@Component({
  selector: "app-workspace-switcher",
  standalone: true,
  imports: [MatIconModule, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="hh-workspace-switcher"
      (mouseenter)="open.set(true)"
      (mouseleave)="closeOnPointerLeave()"
      (focusin)="open.set(true)"
      (focusout)="closeOnPointerLeave()"
      tabindex="-1"
    >
      <button
        #trigger
        type="button"
        class="hh-workspace-trigger"
        [attr.aria-expanded]="open()"
        aria-haspopup="listbox"
        [attr.aria-controls]="menuId"
        [attr.aria-label]="triggerLabel()"
        [attr.title]="triggerLabel()"
        (click)="toggle()"
        (keydown)="onTriggerKeydown($event)"
      >
        <mat-icon class="hh-workspace-icon" aria-hidden="true">{{ activeIcon() }}</mat-icon>
        <span class="hh-workspace-label">{{ activeLabel() }}</span>
        <mat-icon class="hh-workspace-chevron" aria-hidden="true">expand_more</mat-icon>
      </button>
      @if (open()) {
        <div
          [id]="menuId"
          class="hh-workspace-menu"
          role="listbox"
          [attr.aria-label]="optionsLabel()"
          (keydown)="onMenuKeydown($event)"
          tabindex="0"
        >
          @for (option of options(); track option.id) {
            <button
              #option
              type="button"
              role="option"
              class="hh-workspace-option"
              [attr.aria-selected]="activeId() === option.id"
              [tabIndex]="activeId() === option.id ? 0 : -1"
              (click)="select(option.id)"
            >
              <mat-icon class="hh-workspace-icon" aria-hidden="true">{{ option.icon }}</mat-icon>
              <span class="hh-workspace-option-label">{{ option.labelKey | hhTranslate: option.fallback }}</span>
            </button>
          }
        </div>
      }
    </div>
  `,
  styles: [
    `
      :host { display: inline-block; z-index: 30; }
      .hh-workspace-switcher { position: relative; display: inline-block; padding-bottom: var(--space-xs); color: inherit; }
      .hh-workspace-trigger, .hh-workspace-option {
        display: inline-flex; align-items: center; gap: var(--space-sm); min-height: var(--touch-target);
        border: 1px solid currentColor; border-radius: var(--radius-button); padding: 0 var(--space-md);
        background: transparent; color: inherit; font: inherit; cursor: pointer; box-sizing: border-box;
      }
      .hh-workspace-trigger { max-width: 13rem; justify-content: flex-start; }
      .hh-workspace-trigger:focus-visible, .hh-workspace-option:focus-visible {
        outline: var(--focus-ring-width) solid var(--color-focus); outline-offset: var(--focus-ring-width);
      }
      .hh-workspace-option {
        width: 100%; min-height: var(--menu-item-height); justify-content: flex-start;
        border: 0; border-radius: var(--radius-button); padding: var(--space-sm) var(--space-md); text-align: start;
      }
      .hh-workspace-option:hover, .hh-workspace-option:focus-visible { background: var(--surface-hover); outline: 0; }
      .hh-workspace-option[aria-selected="true"] { background: var(--color-primary-soft); color: var(--color-primary-deep); }
      .hh-workspace-icon { width: var(--size-nav-icon-track); height: var(--size-nav-icon-track); flex: 0 0 var(--size-nav-icon-track); font-size: var(--font-size-icon-sm); }
      .hh-workspace-label { flex: 1; min-width: 0; overflow: hidden; font-size: var(--font-size-label); font-weight: var(--font-weight-semibold); letter-spacing: var(--tracking-label); text-overflow: ellipsis; white-space: nowrap; }
      .hh-workspace-chevron { width: var(--font-size-icon-sm); height: var(--font-size-icon-sm); flex: 0 0 var(--font-size-icon-sm); font-size: var(--font-size-subhead); opacity: .88; }
      .hh-workspace-option-label { flex: 1; min-width: 0; color: var(--text-secondary); font-size: var(--font-size-body); font-weight: var(--font-weight-regular); text-align: start; white-space: nowrap; }
      .hh-workspace-option[aria-selected="true"] .hh-workspace-option-label { color: inherit; font-weight: var(--font-weight-semibold); }
      .hh-workspace-menu { position: absolute; top: calc(100% - 1px); inset-inline-start: 0; z-index: 20; display: flex; flex-direction: column; align-items: stretch; gap: var(--menu-item-gap); box-sizing: border-box; width: min(16rem, calc(100vw - var(--space-3xl))); max-height: min(18rem, 70vh); padding: var(--menu-item-gap); overflow-y: auto; border: 1px solid var(--border-default); border-radius: var(--radius-card); background: var(--surface-white); box-shadow: var(--shadow-dropdown); }
      @media (max-width: 640px) { .hh-workspace-menu { position: fixed; top: var(--shell-header-height); left: var(--space-md); width: min(16rem, calc(100vw - var(--space-3xl))); max-height: calc(100vh - 80px); } }
    `,
  ],
})
export class WorkspaceSwitcherComponent {
  readonly options = input<readonly WorkspaceSwitcherOption[]>([]);
  readonly activeId = input<string | null>(null);
  readonly selectionChange = output<string>();
  readonly open = signal(false);
  readonly menuId = `hh-workspace-menu-${Math.random().toString(36).slice(2, 9)}`;

  private readonly i18n = inject(HisHopeI18nService);

  @ViewChildren("option") private readonly optionButtons!: QueryList<ElementRef<HTMLButtonElement>>;
  @ViewChildren("trigger") private readonly triggerButtons!: QueryList<ElementRef<HTMLButtonElement>>;

  activeLabel(): string {
    this.i18n.locale();
    const option = this.options().find((candidate) => candidate.id === this.activeId()) ?? this.options()[0];
    return option ? this.i18n.t(option.labelKey, option.fallback) : this.i18n.t("customerPortal.workspace", "Workspace");
  }

  activeIcon(): string { return this.options().find((option) => option.id === this.activeId())?.icon ?? "dashboard"; }
  triggerLabel(): string { return this.i18n.t("customerPortal.selectWorkspace", "Select workspace"); }
  optionsLabel(): string { return this.i18n.t("customerPortal.workspaceOptions", "Workspace options"); }

  select(id: string): void { this.selectionChange.emit(id); this.open.set(false); this.focusTrigger(); }
  toggle(): void { this.open.update((value) => !value); if (this.open()) queueMicrotask(() => this.focusSelectedOption()); }
  closeOnPointerLeave(): void { this.open.set(false); }

  onTriggerKeydown(event: KeyboardEvent): void {
    if (!["ArrowDown", "Enter", " "].includes(event.key)) return;
    event.preventDefault(); if (!this.open()) this.open.set(true); queueMicrotask(() => this.focusSelectedOption());
  }

  onMenuKeydown(event: KeyboardEvent): void {
    const buttons = this.optionButtons?.toArray() ?? []; if (!buttons.length) return;
    const currentIndex = buttons.findIndex((button) => button.nativeElement === document.activeElement);
    let nextIndex = currentIndex;
    if (event.key === "ArrowDown") nextIndex = (currentIndex + 1) % buttons.length;
    else if (event.key === "ArrowUp") nextIndex = (currentIndex - 1 + buttons.length) % buttons.length;
    else if (event.key === "Home") nextIndex = 0;
    else if (event.key === "End") nextIndex = buttons.length - 1;
    else if (event.key === "Escape") { event.preventDefault(); this.open.set(false); this.focusTrigger(); return; }
    else if (event.key === "Tab") { this.open.set(false); return; }
    else return;
    event.preventDefault(); buttons[nextIndex].nativeElement.focus();
  }

  @HostListener("document:keydown.escape") closeOnEscape(): void { if (this.open()) { this.open.set(false); this.focusTrigger(); } }

  private focusSelectedOption(): void {
    const buttons = this.optionButtons?.toArray() ?? [];
    const index = this.options().findIndex((option) => option.id === this.activeId());
    buttons[index < 0 ? 0 : index]?.nativeElement.focus();
  }
  private focusTrigger(): void { this.triggerButtons?.first?.nativeElement.focus(); }
}
