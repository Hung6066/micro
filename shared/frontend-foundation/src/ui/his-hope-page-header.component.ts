import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'hh-page-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-header">
      <div>
        <h1 class="page-title">{{ title() }}</h1>
        @if (subtitle()) {
          <p class="page-subtitle">{{ subtitle() }}</p>
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

