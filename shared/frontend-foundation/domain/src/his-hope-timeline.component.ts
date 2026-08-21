import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export interface HisHopeTimelineItem { id: string; title: string; detail?: string; date?: string; }

@Component({
  selector: 'hh-timeline',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ol>
      @for (item of items(); track item.id) {
        <li>
          <span class="hh-timeline__dot" aria-hidden="true"></span>
          <div>
            <strong>{{ item.title }}</strong>
            @if (item.date) { <time>{{ item.date }}</time> }
            @if (item.detail) { <p>{{ item.detail }}</p> }
          </div>
        </li>
      }
    </ol>
  `,
  styles: [`
    :host { display: block; }
    ol { list-style: none; margin: 0; padding: 0; }
    li {
      position: relative;
      display: grid;
      grid-template-columns: var(--size-timeline-rail) 1fr;
      gap: var(--space-md);
      padding: 0 0 var(--space-xl);
    }
    li:not(:last-child)::before {
      content: '';
      position: absolute;
      left: var(--space-compact);
      top: var(--size-timeline-rail);
      bottom: 0;
      border-left: 1px solid var(--border-default);
    }
    .hh-timeline__dot {
      z-index: 1;
      width: var(--size-timeline-dot);
      height: var(--size-timeline-dot);
      margin-top: var(--space-hairline);
      border: var(--focus-ring-width-strong) solid var(--surface-white);
      border-radius: var(--radius-full);
      background: var(--color-primary);
      box-shadow: var(--shadow-dot-ring);
    }
    time, p {
      display: block;
      margin: var(--space-xxs) 0 0;
      color: var(--text-secondary);
      font-size: var(--font-size-caption);
    }
  `],
})
export class HisHopeTimelineComponent { readonly items = input<readonly HisHopeTimelineItem[]>([]); }
