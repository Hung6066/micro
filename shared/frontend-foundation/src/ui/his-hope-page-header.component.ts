import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HisHopeTranslatePipe } from '../i18n/his-hope-translate.pipe';

@Component({
  selector: 'hh-page-header',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div>
        <h1 class="page-title">{{ title() | hhTranslate }}</h1>
        @if (subtitle()) {
          <p class="page-subtitle">{{ subtitle() | hhTranslate }}</p>
        }
      </div>
      <div class="page-header-actions"><ng-content /></div>
    </header>
  `,
})
export class HisHopePageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input('');
}

