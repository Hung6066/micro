import { ChangeDetectorRef, Pipe, PipeTransform, inject } from '@angular/core';
import { HisHopeI18nService } from './his-hope-i18n.service';

@Pipe({ name: 'hhTranslate', standalone: true, pure: false })
export class HisHopeTranslatePipe implements PipeTransform {
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  transform(key: string, fallback = key, params: Record<string, string | number> = {}): string { this.cdr.markForCheck(); return this.i18n.t(key, fallback, params); }
}
