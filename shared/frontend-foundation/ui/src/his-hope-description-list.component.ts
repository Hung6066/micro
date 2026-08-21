import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export interface HisHopeDescriptionItem {
  term: string;
  description: string;
}

@Component({
  selector: 'hh-description-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dl>@for (item of items(); track item.term) { <div><dt>{{ item.term }}</dt><dd>{{ item.description }}</dd></div> }</dl>`,
  styles: [`
    :host { display: block; }
    dl { display: grid; grid-template-columns: repeat(auto-fit, minmax(var(--size-description-min), 1fr)); gap: var(--space-lg) var(--space-2xl); margin: 0; }
    dl > div { min-width: 0; }
    dt { color: var(--text-secondary); font-size: var(--font-size-caption); font-weight: var(--font-weight-semibold); }
    dd { margin: var(--space-xxs) 0 0; color: var(--text-primary); overflow-wrap: anywhere; }
  `],
})
export class HisHopeDescriptionListComponent {
  readonly items = input<readonly HisHopeDescriptionItem[]>([]);
}