import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type HisHopeStatusTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

@Component({
  selector: 'hh-status-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="status-badge" [class]="'status-badge status-' + toneClass()"
          role="status" [attr.aria-label]="ariaLabel() || label()">
      <span class="status-badge__dot" aria-hidden="true"></span>
      <span>{{ label() }}</span>
    </span>
  `,
  styles: [`
    :host { display: inline-flex; }
    .status-badge { display: inline-flex; align-items: center; gap: 6px; }
    .status-badge__dot { width: 6px; height: 6px; border-radius: 50%; background: currentColor; }
  `],
})
export class HisHopeStatusBadgeComponent {
  readonly status = input.required<string>();
  readonly label = input.required<string>();
  readonly tone = input<HisHopeStatusTone>('neutral');
  readonly ariaLabel = input('');

  toneClass(): HisHopeStatusTone {
    if (this.tone() !== 'neutral') return this.tone();
    const status = this.status().toLowerCase();
    if (/(healthy|running|active|success|approved|completed|đang chạy|hoạt động)/.test(status)) return 'success';
    if (/(error|failed|stopped|inactive|denied|unhealthy|lỗi|dừng)/.test(status)) return 'danger';
    if (/(warning|pending|degraded|chờ|cảnh báo)/.test(status)) return 'warning';
    return 'neutral';
  }
}
