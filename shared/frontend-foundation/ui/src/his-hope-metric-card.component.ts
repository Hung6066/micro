import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'hh-metric-card',
  standalone: true,
  imports: [RouterModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a [class]="'metric-card tone-' + tone()" [routerLink]="link()" [attr.aria-label]="label()">
      <div class="metric-card__top">
        <span class="metric-icon material-icons" aria-hidden="true">{{ icon() }}</span>
        <span class="metric-label">{{ label() }}</span>
      </div>
      <strong class="metric-value">{{ value() }}</strong>
      @if (action()) {
        <span class="metric-action">{{ action() }} <span class="material-icons" aria-hidden="true">arrow_forward</span></span>
      }
    </a>
  `,
  styles: [`
    :host { display: block; min-width: 0; font-family: var(--font-sans); }
    .metric-card {
      display: flex;
      min-height: 156px;
      flex-direction: column;
      gap: var(--space-lg);
      padding: var(--space-lg);
      border: 1px solid var(--border-default);
      border-radius: var(--radius-card);
      background: var(--surface-white);
      color: var(--text-primary);
      text-decoration: none;
      transition: border-color 150ms ease, box-shadow 150ms ease, transform 150ms ease;
    }
    .metric-card:hover { border-color: var(--color-primary); box-shadow: var(--shadow-metric-hover); transform: translateY(calc(var(--space-hairline) * -1)); }
    .metric-card:active { transform: translateY(0); }
    .metric-card:focus-visible { outline: var(--focus-ring-width) solid var(--color-focus); outline-offset: var(--focus-ring-width); }
    .metric-card__top { display: flex; align-items: center; gap: var(--space-md); }
    .metric-icon {
      display: grid;
      width: var(--control-height-compact);
      height: var(--control-height-compact);
      place-items: center;
      border-radius: var(--radius-card);
      background: var(--color-primary-soft);
      color: var(--color-primary);
      font-size: var(--font-size-section);
    }
    .tone-info .metric-icon { background: var(--surface-info); color: var(--color-info); }
    .tone-warning .metric-icon { background: var(--surface-warning); color: var(--color-warning); }
    .tone-danger .metric-icon { background: var(--surface-danger); color: var(--color-danger); }
    .metric-label { color: var(--text-secondary); font-size: var(--font-size-caption); font-weight: var(--font-weight-semibold); }
    .metric-value { min-height: var(--page-padding-block); font-size: var(--font-size-title); font-weight: var(--font-weight-bold); line-height: 1.2; }
    .metric-action { display: inline-flex; align-items: center; gap: var(--space-2xs); margin-top: auto; color: var(--color-primary); font-size: var(--font-size-label); font-weight: var(--font-weight-semibold); }
    .metric-action .material-icons { font-size: var(--font-size-input); }
  `],
})
export class HisHopeMetricCardComponent {
  readonly icon = input.required<string>();
  readonly label = input.required<string>();
  readonly value = input.required<string | number>();
  readonly link = input.required<string>();
  readonly action = input('');
  readonly tone = input('default');
}
