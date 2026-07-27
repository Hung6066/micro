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
      gap: 14px;
      padding: 18px;
      border: 1px solid var(--border-default);
      border-radius: var(--radius-card);
      background: var(--surface-white);
      color: var(--text-primary);
      text-decoration: none;
      transition: border-color 150ms ease, box-shadow 150ms ease, transform 150ms ease;
    }
    .metric-card:hover { border-color: var(--color-primary); box-shadow: 0 8px 22px rgba(47, 107, 74, .10); transform: translateY(-2px); }
    .metric-card:active { transform: translateY(0); }
    .metric-card:focus-visible { outline: 2px solid var(--color-focus); outline-offset: 2px; }
    .metric-card__top { display: flex; align-items: center; gap: 10px; }
    .metric-icon {
      display: grid;
      width: 36px;
      height: 36px;
      place-items: center;
      border-radius: 8px;
      background: var(--color-primary-soft);
      color: var(--color-primary);
      font-size: 20px;
    }
    .tone-info .metric-icon { background: var(--surface-info); color: var(--color-info); }
    .tone-warning .metric-icon { background: var(--surface-warning); color: var(--color-warning); }
    .tone-danger .metric-icon { background: var(--surface-danger); color: var(--color-danger); }
    .metric-label { color: var(--text-secondary); font-size: 12px; font-weight: 600; }
    .metric-value { min-height: 29px; font-size: 24px; font-weight: 700; line-height: 1.2; }
    .metric-action { display: inline-flex; align-items: center; gap: 4px; margin-top: auto; color: var(--color-primary); font-size: 13px; font-weight: 600; }
    .metric-action .material-icons { font-size: 16px; }
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
