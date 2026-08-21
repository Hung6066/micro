import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'hh-form-layout',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hh-form-layout" [class.hh-form-layout--single-column]="columns() === 1" [class.hh-form-layout--dense]="density() === 'dense'">
      <ng-content />
    </div>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .hh-form-layout { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--space-xl) var(--space-2xl); align-items: start; }
    .hh-form-layout--single-column { grid-template-columns: minmax(0, 1fr); }
    .hh-form-layout--dense { gap: var(--space-lg); }
    .hh-form-layout ::ng-deep .hh-form-field--span-2,
    .hh-form-layout ::ng-deep .form-field--span-2 { grid-column: 1 / -1; }
    @media (max-width: 720px) { .hh-form-layout { grid-template-columns: minmax(0, 1fr); gap: var(--space-lg); } }
  `],
})
export class HisHopeFormLayoutComponent {
  readonly columns = input<1 | 2>(2);
  readonly density = input<'comfortable' | 'dense'>('comfortable');
}
