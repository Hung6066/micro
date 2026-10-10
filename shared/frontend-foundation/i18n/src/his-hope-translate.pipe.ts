import { ChangeDetectorRef, Pipe, PipeTransform } from '@angular/core';
import { HisHopeI18nService } from './his-hope-i18n.service';

@Pipe({ name: 'hhTranslate', standalone: true, pure: false })
export class HisHopeTranslatePipe implements PipeTransform {
  constructor(private readonly i18n: HisHopeI18nService, private readonly cdr: ChangeDetectorRef) {}
  transform(key: string, fallback = key, params: Record<string, string | number> = {}): string {
    this.i18n.locale();
    this.cdr.markForCheck();
    return this.i18n.t(key, fallback, params);
  }
}
