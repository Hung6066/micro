import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

@Component({
  selector: 'hh-workspace-header',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="hh-workspace-header">
      <div>
        <div class="hh-workspace-header__eyebrow">{{ eyebrow() | hhTranslate }}</div>
        <h1>{{ title() | hhTranslate }}</h1>
      </div>
      <div class="hh-workspace-header__status">{{ status() | hhTranslate }}</div>
    </header>
  `,
})
export class HisHopeWorkspaceHeaderComponent {
  readonly eyebrow = input('Clinical workspace');
  readonly title = input('Patient care operations');
  readonly status = input('Secure session active');
}
