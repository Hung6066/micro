import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HisHopeMobileIconComponent, HisHopeMobileIconName } from './his-hope-mobile-icon.component';
import { HisHopeTranslatePipe } from '../i18n/his-hope-translate.pipe';

export type HisHopeStateKind = 'loading' | 'empty' | 'error' | 'offline' | 'forbidden';

@Component({
  selector: 'hh-state',
  standalone: true,
  imports: [HisHopeMobileIconComponent, HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-state" [class]="'hh-state--' + kind()"
             [attr.role]="kind() === 'error' || kind() === 'offline' || kind() === 'forbidden' ? 'alert' : 'status'"
             [attr.aria-live]="kind() === 'error' || kind() === 'offline' || kind() === 'forbidden' ? 'assertive' : 'polite'">
      @if (kind() === 'loading') {
        <span class="hh-spinner" aria-hidden="true"></span>
      } @else {
        <hh-mobile-icon class="hh-state-icon" [name]="stateIcon()" size="large" aria-hidden="true" />
      }
      <p>{{ message() | hhTranslate }}</p>
      @if (detail()) { <small>{{ detail() | hhTranslate }}</small> }
      <ng-content />
    </section>
  `,
})
export class HisHopeStateComponent {
  readonly kind = input<HisHopeStateKind>('empty');
  readonly message = input('state.notFound');
  readonly detail = input('');
  readonly icon = input('inbox');

  stateIcon(): HisHopeMobileIconName {
    if (this.kind() === 'offline') return 'offline';
    if (this.kind() === 'forbidden') return 'forbidden';
    if (this.kind() === 'error') return 'error';
    return 'empty';
  }
}
