import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'hh-toolbar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-toolbar" [attr.aria-label]="label()">
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
