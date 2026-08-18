import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

@Component({
  selector: 'hh-form-section',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class.hh-form-section--span-2]': 'span() === 2',
  },
  template: `
    <fieldset class="hh-form-section">
      @if (title()) {
        <legend class="hh-form-section__title">{{ title() | hhTranslate }}</legend>
      }
      @if (description()) {
        <p class="hh-form-section__description">{{ description() | hhTranslate }}</p>
      }
      <div class="hh-form-section__content"><ng-content /></div>
    </fieldset>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .hh-form-section { min-width: 0; margin: 0; padding: 0; border: 0; }
    :host(.hh-form-section--span-2) { grid-column: 1 / -1; }
    .hh-form-section__title { padding: 0; color: var(--text-primary); font-size: var(--font-size-label); line-height: 20px; font-weight: var(--font-weight-semibold); }
    .hh-form-section__description { margin: var(--space-1) 0 var(--space-3); color: var(--text-secondary); font-size: var(--font-size-caption); line-height: var(--leading-body); }
    .hh-form-section__content { display: grid; gap: var(--form-field-gap); min-width: 0; }
  `],
})
export class HisHopeFormSectionComponent {
  readonly title = input('');
  readonly description = input('');
  readonly span = input<1 | 2>(1);
}
