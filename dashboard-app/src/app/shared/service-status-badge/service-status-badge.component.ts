import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

export type ServiceStatus = 'Running' | 'Stopped' | 'Degraded' | 'Unknown' | string;

const STATUS_CONFIG: Record<string, { color: string; bg: string; label: string }> = {
  Running:   { color: '#2F6B4A', bg: '#EDF3EC', label: 'Đang chạy' },
  Healthy:   { color: '#2F6B4A', bg: '#EDF3EC', label: 'Khỏe mạnh' },
  Stopped:   { color: '#787774', bg: '#F0F0EE', label: 'Đã dừng' },
  Degraded:  { color: '#B6581C', bg: '#FDF0E2', label: 'Suy giảm' },
  Unknown:   { color: '#C25450', bg: '#FDEBEC', label: 'Không xác định' },
  Unhealthy: { color: '#C25450', bg: '#FDEBEC', label: 'Mất sức khỏe' },
};

@Component({
  selector: 'app-service-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="status-badge" [style.--dot-color]="config.color" [style.--badge-bg]="config.bg">
      <span class="status-dot"></span>
      <span class="status-label">{{ config.label }}</span>
    </span>
  `,
  styles: [`
    .status-badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 2px 10px 2px 8px;
      border-radius: 4px;
      font-size: 11px;
      font-weight: 500;
      line-height: 1.6;
      white-space: nowrap;
      letter-spacing: 0.02em;
      background: var(--badge-bg, #F0F0EE);
    }
    .status-dot {
      display: inline-block;
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: var(--dot-color, #787774);
      flex-shrink: 0;
    }
    .status-label {
      color: var(--dot-color, #787774);
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceStatusBadgeComponent {
  @Input({ required: true }) set state(value: ServiceStatus) {
    this.config = STATUS_CONFIG[value] ?? STATUS_CONFIG['Unknown'];
  }

  config = STATUS_CONFIG['Unknown'];
}
