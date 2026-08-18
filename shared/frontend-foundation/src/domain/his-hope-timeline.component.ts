import { ChangeDetectionStrategy, Component, input } from "@angular/core";

export interface HisHopeTimelineItem {
  id: string;
  title: string;
  detail?: string;
  date?: string;
}

@Component({
  selector: "hh-timeline",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<ol>
    @for (item of items(); track item.id) {
      <li>
        <span class="hh-timeline__dot" aria-hidden="true"></span>
        <div>
          <strong>{{ item.title }}</strong>
          @if (item.date) {
            <time>{{ item.date }}</time>
          }
          @if (item.detail) {
            <p>{{ item.detail }}</p>
          }
        </div>
      </li>
    }
  </ol>`,
  styles: [
    `
      :host {
        display: block;
      }
      ol {
        list-style: none;
        margin: 0;
        padding: 0;
      }
      li {
        position: relative;
        display: grid;
        grid-template-columns: 16px 1fr;
        gap: 12px;
        padding: 0 0 20px;
      }
      li:not(:last-child)::before {
        content: "";
        position: absolute;
        left: 7px;
        top: 16px;
        bottom: 0;
        border-left: 1px solid var(--border-default);
      }
      .hh-timeline__dot {
        z-index: 1;
        width: 14px;
        height: 14px;
        margin-top: 2px;
        border: 3px solid var(--surface-white);
        border-radius: 50%;
        background: var(--color-primary);
        box-shadow: 0 0 0 1px var(--color-primary);
      }
      time,
      p {
        display: block;
        margin: 3px 0 0;
        color: var(--text-secondary);
        font-size: var(--font-size-caption);
      }
    `,
  ],
})
export class HisHopeTimelineComponent {
  readonly items = input<readonly HisHopeTimelineItem[]>([]);
}
