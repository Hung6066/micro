import { ChangeDetectionStrategy, Component, EventEmitter, Output, input } from '@angular/core';

@Component({
  selector: 'hh-mobile-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="hh-mobile-list" role="list" [attr.aria-label]="label()"><ng-content /></div>`,
  styles: [`
    :host { display: block; }
    .hh-mobile-list { overflow: hidden; border: 1px solid var(--border-default); border-radius: var(--radius-card); background: var(--surface-white); }
    .hh-mobile-list ::ng-deep hh-mobile-list-item + hh-mobile-list-item { border-top: 1px solid var(--border-light); }
  `],
})
export class HisHopeMobileListComponent {
  readonly label = input('List');
}

@Component({
  selector: 'hh-mobile-list-item',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hh-mobile-list-item" [class.hh-mobile-list-item--selected]="selected()" [class.hh-mobile-list-item--disabled]="disabled()" [class.hh-mobile-list-item--resource]="variant() === 'resource'" [class.hh-mobile-list-item--action]="variant() === 'action'" [class.hh-mobile-list-item--setting]="variant() === 'setting'" [class.hh-mobile-list-item--expandable]="variant() === 'expandable'" [class.hh-mobile-list-item--danger]="variant() === 'danger'" role="listitem">
      <button class="hh-mobile-list-item__main" type="button" [disabled]="disabled()" [attr.aria-pressed]="selected() ? 'true' : null" [attr.aria-expanded]="variant() === 'expandable' ? expanded : null" (click)="activate()">
        <span class="hh-mobile-list-item__leading"><ng-content select="[hhMobileItemLeading]" /></span>
        <span class="hh-mobile-list-item__content">
          <span class="hh-mobile-list-item__title"><ng-content select="[hhMobileItemTitle]" /></span>
          <span class="hh-mobile-list-item__description"><ng-content select="[hhMobileItemDescription]" /></span>
          <span class="hh-mobile-list-item__meta"><ng-content select="[hhMobileItemMeta]" /></span>
        </span>
        <span class="hh-mobile-list-item__status"><ng-content select="[hhMobileItemStatus]" /></span>
        <span class="hh-mobile-list-item__trailing"><ng-content select="[hhMobileItemTrailing]" /></span>
      </button>
      <span class="hh-mobile-list-item__action"><ng-content select="[hhMobileItemAction]" /></span>
      @if (variant() === 'expandable' && expanded) { <div class="hh-mobile-list-item__detail" role="region"><ng-content select="[hhMobileItemDetail]" /></div> }
    </div>
  `,
  styles: [`
    :host { display: block; }
    .hh-mobile-list-item { display: flex; flex-wrap: wrap; align-items: stretch; min-height: var(--shell-header-height); background: var(--surface-white); }
    .hh-mobile-list-item__main { display: flex; align-items: center; gap: var(--space-md); flex: 1; min-width: 0; min-height: var(--shell-header-height); padding: var(--space-md) var(--space-lg); border: 0; background: transparent; color: var(--text-primary); font: inherit; text-align: left; cursor: pointer; }
    .hh-mobile-list-item__main:focus-visible { position: relative; z-index: 1; outline: var(--focus-ring-width-strong) solid color-mix(in srgb, var(--color-primary) 35%, transparent); outline-offset: -var(--focus-ring-width-strong); }
    .hh-mobile-list-item__main:disabled { cursor: not-allowed; }
    .hh-mobile-list-item__leading, .hh-mobile-list-item__status, .hh-mobile-list-item__trailing, .hh-mobile-list-item__action { display: inline-flex; align-items: center; justify-content: center; flex: 0 0 auto; }
    .hh-mobile-list-item__leading:empty, .hh-mobile-list-item__status:empty, .hh-mobile-list-item__trailing:empty, .hh-mobile-list-item__action:empty { display: none; }
    .hh-mobile-list-item__content { display: grid; gap: var(--space-xxs); flex: 1; min-width: 0; }
    .hh-mobile-list-item__title, .hh-mobile-list-item__description, .hh-mobile-list-item__meta { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .hh-mobile-list-item__title { font-size: var(--font-size-body); font-weight: var(--font-weight-semibold); }
    .hh-mobile-list-item__description { color: var(--text-secondary); font-size: var(--font-size-caption); }
    .hh-mobile-list-item__meta { color: var(--text-muted); font-size: var(--font-size-nav); }
    .hh-mobile-list-item__trailing { color: var(--text-secondary); }
    .hh-mobile-list-item__action { padding-right: var(--space-sm); }
    .hh-mobile-list-item__detail { flex: 0 0 100%; padding: 0 var(--space-lg) var(--space-lg) var(--shell-header-height); color: var(--text-secondary); font-size: var(--font-size-label); line-height: 1.5; }
    .hh-mobile-list-item--action .hh-mobile-list-item__main { min-height: 72px; }
    .hh-mobile-list-item--setting .hh-mobile-list-item__main { min-height: var(--mobile-toolbar-height); }
    .hh-mobile-list-item--danger .hh-mobile-list-item__title { color: var(--color-danger, #b3261e); }
    .hh-mobile-list-item--danger .hh-mobile-list-item__leading { color: var(--color-danger, #b3261e); }
    .hh-mobile-list-item--selected { background: var(--color-primary-soft); }
    .hh-mobile-list-item--disabled { opacity: .55; }
  `],
})
export class HisHopeMobileListItemComponent {
  readonly variant = input<'resource' | 'action' | 'setting' | 'expandable' | 'danger'>('resource');
  readonly selected = input(false);
  readonly disabled = input(false);
  expanded = false;
  @Output() readonly activated = new EventEmitter<void>();
  activate(): void {
    if (this.disabled()) return;
    if (this.variant() === 'expandable') this.expanded = !this.expanded;
    this.activated.emit();
  }
}
