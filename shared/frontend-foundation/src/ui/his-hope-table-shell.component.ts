import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HisHopeTranslatePipe } from '../i18n/his-hope-translate.pipe';

@Component({
  selector: 'hh-table-shell',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="hh-table-shell" [attr.aria-label]="label() | hhTranslate">
      <div class="hh-table-shell__content"><ng-content /></div>
    </section>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .hh-table-shell {
      overflow: hidden;
      border: 1px solid var(--border-default);
      border-radius: var(--radius-card);
      background: var(--surface-white);
    }
    .hh-table-shell__content { min-width: 0; overflow-x: auto; }
    .hh-table-shell table { width: 100%; min-width: 640px; }
    @media (max-width: 768px) {
      .hh-table-shell { border-radius: var(--radius-input); }
    }
  `],
})
export class HisHopeTableShellComponent {
  readonly label = input('common.dataTable');
}
