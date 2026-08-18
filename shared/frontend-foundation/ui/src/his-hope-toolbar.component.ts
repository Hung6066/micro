import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

@Component({
  selector: 'hh-toolbar',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-toolbar" [attr.aria-label]="label() | hhTranslate">
      <div class="hh-toolbar__title"><ng-content select="[hhToolbarTitle]" /></div>
      <div class="hh-toolbar__leading">
        <ng-content select="[hh-toolbar-leading]" />
      </div>
      <div class="hh-toolbar__controls">
        <ng-content select="[hh-toolbar-controls]" />
      </div>
      <div class="hh-toolbar__actions">
        <ng-content select="[hh-toolbar-actions]" />
        <ng-content />
      </div>
    </section>
  `,
})
export class HisHopeToolbarComponent {
  readonly label = input('Toolbar');
}
