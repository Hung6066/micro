import { Pipe, PipeTransform, inject } from '@angular/core';
import { HisHopeI18nService } from '@his-hope/frontend-foundation/i18n';

@Pipe({
  name: 'relativeTime',
  standalone: true,
})
export class RelativeTimePipe implements PipeTransform {
  private readonly i18n = inject(HisHopeI18nService);

  transform(value: Date | string | number | null | undefined): string {
    if (!value) return '';
    const date = value instanceof Date ? value : new Date(value);
    const now = Date.now();
    const diffMs = now - date.getTime();
    const diffSec = Math.floor(diffMs / 1000);

    if (diffSec < 60)
      return `${this.i18n.t('dashboard.time.secondsAgo', `${diffSec}s trước`, { count: diffSec })}`;
    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60)
      return `${this.i18n.t('dashboard.time.minutesAgo', `${diffMin}ph trước`, { count: diffMin })}`;
    const diffHr = Math.floor(diffMin / 60);
    if (diffHr < 24)
      return `${this.i18n.t('dashboard.time.hoursAgo', `${diffHr}giờ trước`, { count: diffHr })}`;
    const diffDay = Math.floor(diffHr / 24);
    if (diffDay < 30)
      return `${this.i18n.t('dashboard.time.daysAgo', `${diffDay} ngày trước`, { count: diffDay })}`;
    return date.toLocaleDateString('vi-VN');
  }
}
