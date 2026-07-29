import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HisHopeTranslatePipe } from '../i18n/his-hope-translate.pipe';

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
    :host { display: block; color: var(--text-primary); font-family: var(--font-sans); }
    .brand { display: flex; align-items: center; gap: 10px; }
    .brand-mark {
      display: grid;
      place-items: center;
      width: 32px;
      height: 32px;
      border-radius: 9px;
      background: var(--color-primary);
      color: #fff;
      flex: 0 0 auto;
    }
    .brand-mark::before,
    .brand-mark::after,
    .brand-mark span {
      content: '';
      position: absolute;
      display: block;
      border-radius: 2px;
      background: #fff;
    }
    .brand-mark { position: relative; }
    .brand-mark::before { width: 14px; height: 4px; }
    .brand-mark::after { width: 4px; height: 14px; }
    .brand-mark span { width: 4px; height: 4px; opacity: .9; }
    .brand-name { font-size: 18px; font-weight: 700; letter-spacing: 0; }
    .brand-caption {
      margin: 12px 0 0;
      color: var(--text-muted);
      font-size: 10px;
      font-weight: 700;
      letter-spacing: .12em;
      line-height: 1.3;
      text-transform: uppercase;
    }
  `],
})
export class HisHopeBrandComponent {
  readonly caption = input('');
}
