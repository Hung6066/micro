import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'hh-workspace-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="hh-workspace-header">
      <div>
        <div class="hh-workspace-header__eyebrow">{{ eyebrow() }}</div>
        <h1>{{ title() }}</h1>
      </div>
      <div class="hh-workspace-header__status">{{ status() }}</div>
    </header>
  `,
})
export class HisHopeWorkspaceHeaderComponent {
  readonly eyebrow = input('Clinical workspace');
  readonly title = input('Patient care operations');
  readonly status = input('Secure session active');
}
