import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

@Component({
  selector: 'hh-brand',
  standalone: true,
  imports: [HisHopeTranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="brand" aria-label="His.Hope">
      <span class="brand-mark" aria-hidden="true"><span></span></span>
      <span class="brand-name">{{ 'app.name' | hhTranslate }}</span>
    </div>
    @if (caption()) {
      <div class="brand-caption">{{ caption() | hhTranslate }}</div>
    }
  `,
  styles: [`
    :host {
      display: block;
      color: var(--text-primary);
      font-family: var(--font-sans);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: var(--space-md);
    }
    .brand-mark {
      --brand-mark-size: var(--space-3xl);
      --brand-cross-long: var(--font-size-body);
      --brand-cross-short: var(--space-2xs);
      --brand-cross-dot: var(--space-2xs);
      position: relative;
      display: grid;
      place-items: center;
      flex: 0 0 auto;
      width: var(--brand-mark-size);
      height: var(--brand-mark-size);
      border-radius: var(--radius-brand-mark);
      background: var(--color-primary);
      color: var(--color-on-primary);
    }
    .brand-mark::before,
    .brand-mark::after,
    .brand-mark span {
      content: '';
      position: absolute;
      display: block;
      border-radius: var(--radius-micro);
      background: var(--color-on-primary);
    }
    .brand-mark::before {
      width: var(--brand-cross-long);
      height: var(--brand-cross-short);
    }
    .brand-mark::after {
      width: var(--brand-cross-short);
      height: var(--brand-cross-long);
    }
    .brand-mark span {
      width: var(--brand-cross-dot);
      height: var(--brand-cross-dot);
      opacity: 0.9;
    }
    .brand-name {
      font-size: var(--font-size-subhead);
      font-weight: var(--font-weight-bold);
      letter-spacing: 0;
    }
    .brand-caption {
      margin: var(--space-md) 0 0;
      color: var(--text-muted);
      font-size: var(--font-size-overline);
      font-weight: var(--font-weight-bold);
      letter-spacing: 0.12em;
      line-height: 1.3;
      text-transform: uppercase;
    }
  `],
})
export class HisHopeBrandComponent {
  readonly caption = input('');
}
