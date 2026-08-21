import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type HisHopePageDensity = 'comfortable' | 'dense';

@Component({
  selector: 'hh-page-layout',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="hh-page-layout" [class.hh-page-layout--dense]="density() === 'dense'">
      <div class="hh-page-layout__breadcrumb"><ng-content select="[hhPageBreadcrumb]" /></div>
      <div class="hh-page-layout__header"><ng-content select="[hhPageHeader]" /></div>
      <div class="hh-page-layout__toolbar"><ng-content select="[hhPageToolbar]" /></div>
      <div class="hh-page-layout__content"><ng-content /></div>
    </main>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .hh-page-layout { width: 100%; max-width: var(--max-width-container, 1200px); margin: 0 auto; padding: var(--page-padding-y, 28px) var(--page-padding-x, var(--space-3xl)); }
    .hh-page-layout__breadcrumb:empty, .hh-page-layout__header:empty, .hh-page-layout__toolbar:empty { display: none; }
    .hh-page-layout__breadcrumb { margin-bottom: var(--space-md); }
    .hh-page-layout__header { margin-bottom: var(--space-xl); }
    .hh-page-layout__toolbar { margin-bottom: var(--space-lg); }
    .hh-page-layout__content { display: grid; gap: var(--space-xl); min-width: 0; }
    .hh-page-layout--dense { --page-padding-y: var(--space-xl); --page-padding-x: var(--space-2xl); }
    @media (max-width: 768px) {
      .hh-page-layout, .hh-page-layout--dense { padding: var(--space-xl) var(--space-lg); }
      .hh-page-layout__header { margin-bottom: var(--space-lg); }
    }
  `],
})
export class HisHopePageLayoutComponent {
  readonly density = input<HisHopePageDensity>('comfortable');
}

@Component({
  selector: 'hh-page-section',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-page-section" [attr.aria-labelledby]="titleId()">
      <header class="hh-page-section__header">
        <div>
          <h2 [id]="titleId()">{{ title() }}</h2>
          @if (subtitle()) { <p>{{ subtitle() }}</p> }
        </div>
        <div class="hh-page-section__actions"><ng-content select="[hhSectionActions]" /></div>
      </header>
      <div class="hh-page-section__body"><ng-content /></div>
    </section>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .hh-page-section { min-width: 0; border: 1px solid var(--border-default); border-radius: var(--radius-card); background: var(--surface-white); }
    .hh-page-section__header { display: flex; align-items: flex-start; justify-content: space-between; gap: var(--space-lg); padding: var(--space-lg) var(--space-xl) var(--space-md); }
    .hh-page-section__header h2 { margin: 0; color: var(--text-primary); font-size: var(--font-size-section); line-height: 1.35; font-weight: var(--font-weight-semibold); }
    .hh-page-section__header p { margin: var(--space-2xs) 0 0; color: var(--text-secondary); font-size: var(--font-size-caption); }
    .hh-page-section__actions { display: flex; align-items: center; gap: var(--space-sm); flex-wrap: wrap; }
    .hh-page-section__body { min-width: 0; padding: 0 var(--space-xl) var(--space-xl); }
    @media (max-width: 640px) {
      .hh-page-section__header { flex-direction: column; }
      .hh-page-section__actions { width: 100%; }
      .hh-page-section__actions > * { max-width: 100%; }
    }
  `],
})
export class HisHopePageSectionComponent {
  readonly title = input.required<string>();
  readonly subtitle = input('');
  readonly titleId = input('hh-page-section-title');
}

@Component({
  selector: 'hh-meta-item',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hh-meta-item">
      @if (icon()) { <span class="material-icons" aria-hidden="true">{{ icon() }}</span> }
      <div><span class="hh-meta-item__label">{{ label() }}</span><strong>{{ value() }}</strong></div>
    </div>
  `,
  styles: [`
    :host { display: inline-block; min-width: 0; }
    .hh-meta-item { display: inline-flex; align-items: flex-start; gap: var(--space-sm); min-width: 0; }
    .hh-meta-item > .material-icons { width: var(--font-size-icon-sm); height: var(--font-size-icon-sm); color: var(--text-muted); font-size: var(--font-size-icon-sm); }
    .hh-meta-item > div { display: grid; gap: var(--space-hairline); min-width: 0; }
    .hh-meta-item__label { color: var(--text-muted); font-size: var(--font-size-caption); }
    .hh-meta-item strong { overflow-wrap: anywhere; color: var(--text-primary); font-size: var(--font-size-body); font-weight: var(--font-weight-semibold); }
  `],
})
export class HisHopeMetaItemComponent {
  readonly label = input.required<string>();
  readonly value = input.required<string>();
  readonly icon = input('');
}
