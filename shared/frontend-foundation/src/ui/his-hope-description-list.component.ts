import { ChangeDetectionStrategy, Component, input } from "@angular/core";

export interface HisHopeDescriptionItem {
  term: string;
  description: string;
}

@Component({
  selector: "hh-description-list",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<dl>
    @for (item of items(); track item.term) {
      <div>
        <dt>{{ item.term }}</dt>
        <dd>{{ item.description }}</dd>
      </div>
    }
  </dl>`,
  styles: [
    `
      :host {
        display: block;
      }
      dl {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        gap: 14px 24px;
        margin: 0;
      }
      dl > div {
        min-width: 0;
      }
      dt {
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
        font-weight: 600;
      }
      dd {
        margin: 3px 0 0;
        color: var(--text-primary);
        overflow-wrap: anywhere;
      }
    `,
  ],
})
export class HisHopeDescriptionListComponent {
  readonly items = input<readonly HisHopeDescriptionItem[]>([]);
}
