import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'hh-skeleton',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hh-skeleton" aria-hidden="true">
      @for (line of lines(); track $index) {
        <span [style.width]="line"></span>
      }
    </div>
  `,
})
export class HisHopeSkeletonComponent {
  readonly lines = input<string[]>(['92%', '76%', '84%']);
}

