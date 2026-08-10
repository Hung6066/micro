import { ChangeDetectionStrategy, Component, ElementRef, EventEmitter, HostListener, Output, QueryList, ViewChildren, input, signal } from '@angular/core';
import { HisHopeI18nService, HisHopeLocale } from './his-hope-i18n.service';
import { HisHopeLocalizationApiService } from './his-hope-localization-api.service';

@Component({
  selector: 'hh-language-switcher',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="hh-language-switcher" (mouseenter)="open.set(true)" (mouseleave)="closeOnPointerLeave()">
      <button #trigger type="button" class="hh-language-trigger" [attr.aria-expanded]="open()" aria-haspopup="listbox" [attr.aria-controls]="menuId" [attr.aria-label]="label(i18n.locale())" [attr.title]="label(i18n.locale())" (click)="toggle()" (keydown)="onTriggerKeydown($event)">
        <span class="hh-language-icon" [attr.aria-label]="label(i18n.locale())">
          @if (flagSource(i18n.locale()); as flag) { <img class="hh-language-flag" [src]="flag" alt="" aria-hidden="true" /> } @else { <span aria-hidden="true">{{ icon(i18n.locale()) }}</span> }
        </span>
        <span class="hh-language-code" aria-hidden="true">{{ shortCode(i18n.locale()) }}</span>
      </button>
      @if (open()) {
        <div [id]="menuId" class="hh-language-menu" role="listbox" [attr.aria-label]="menuLabel" (keydown)="onMenuKeydown($event)">
          @for (locale of locales(); track locale.code) {
            <button #option type="button" role="option" class="hh-language-option" [attr.aria-selected]="i18n.locale() === locale.code" [tabIndex]="i18n.locale() === locale.code ? 0 : -1" (click)="change(locale.code)">
              <span class="hh-language-icon" [attr.aria-label]="locale.label">
                @if (flagSource(locale.code, locale.flag); as flag) { <img class="hh-language-flag" [src]="flag" alt="" aria-hidden="true" /> } @else { <span aria-hidden="true">{{ locale.icon }}</span> }
              </span>
              <span class="hh-language-code">{{ shortCode(locale.code) }}</span>
              <span class="hh-language-label">{{ locale.label }}</span>
            </button>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    :host { display: inline-block; z-index: 30; }
    .hh-language-switcher { position: relative; display: inline-block; padding-bottom: 6px; color: inherit; }
    .hh-language-trigger, .hh-language-option { display: inline-flex; align-items: center; gap: var(--space-2); min-height: var(--touch-target); border: 1px solid currentColor; border-radius: var(--radius-button); padding: 0 var(--space-3); background: transparent; color: inherit; font: inherit; cursor: pointer; box-sizing: border-box; }
    .hh-language-trigger { min-width: 82px; justify-content: center; }
    .hh-language-trigger:focus-visible, .hh-language-option:focus-visible { outline: 2px solid var(--color-focus); outline-offset: 2px; }
    .hh-language-option { width: 100%; min-height: var(--menu-item-height); justify-content: flex-start; border: 0; border-radius: var(--radius-button); padding: 8px 12px; box-sizing: border-box; text-align: start; }
    .hh-language-option:hover, .hh-language-option:focus-visible { background: var(--surface-hover); outline: 0; }
    .hh-language-option[aria-selected="true"] { background: var(--color-primary-soft); color: var(--color-primary-deep); }
    .hh-language-icon { display: inline-grid; place-items: center; width: 30px; height: 30px; flex: 0 0 30px; overflow: hidden; border: 1px solid var(--border-default); border-radius: 50%; background: var(--surface-white); font-size: 17px; line-height: 1; }
    .hh-language-flag { display: block; width: 100%; height: 100%; object-fit: cover; }
    .hh-language-code { min-width: 25px; font-size: var(--font-size-label); font-weight: var(--font-weight-semibold); letter-spacing: .04em; }
    .hh-language-label { flex: 1; color: var(--text-secondary); font-size: var(--font-size-body); font-weight: var(--font-weight-regular); white-space: nowrap; }
    .hh-language-menu { position: absolute; top: calc(100% - 1px); inset-inline-end: 0; z-index: 20; display: flex; flex-direction: column; align-items: stretch; gap: var(--menu-item-gap); padding: var(--menu-item-gap); box-sizing: border-box; width: min(220px, calc(100vw - 32px)); max-height: min(360px, 70vh); overflow-y: auto; border: 1px solid var(--border-default); border-radius: var(--radius-card); background: var(--surface-white); box-shadow: var(--shadow-dropdown); }
    @media (max-width: 640px) { .hh-language-menu { position: fixed; top: 64px; right: 16px; bottom: auto; left: auto; width: min(210px, calc(100vw - 32px)); max-height: calc(100vh - 80px); } }
  `],
})
export class HisHopeLanguageSwitcherComponent {
  readonly locales = input<HisHopeLanguageOption[]>([
    { code: 'vi-VN', label: 'Tiếng Việt', icon: 'VN' },
    { code: 'en', label: 'English', icon: 'EN' },
  ]);
  constructor(readonly i18n: HisHopeI18nService, readonly localizationApi: HisHopeLocalizationApiService) {}
  readonly open = signal(false);
  readonly triggerId = `hh-language-trigger-${Math.random().toString(36).slice(2, 9)}`;
  readonly menuId = `hh-language-menu-${Math.random().toString(36).slice(2, 9)}`;
  readonly menuLabel = 'Language options';
  @Output() readonly localeChange = new EventEmitter<HisHopeLocale>();
  @ViewChildren('option') private readonly optionButtons!: QueryList<ElementRef<HTMLButtonElement>>;
  @ViewChildren('trigger') private readonly triggerButtons!: QueryList<ElementRef<HTMLButtonElement>>;

  label(locale: HisHopeLocale): string { return this.locales().find(option => option.code === locale)?.label ?? locale; }
  icon(locale: HisHopeLocale): string { return this.locales().find(option => option.code === locale)?.icon ?? locale.slice(0, 2).toUpperCase(); }
  flagSource(locale: HisHopeLocale, custom?: string): string {
    if (custom) return custom;
    const normalized = locale.toLowerCase();
    return HIS_HOPE_FLAG_SOURCES[normalized] ?? HIS_HOPE_FLAG_SOURCES[normalized.split('-')[0]] ?? '';
  }
  shortCode(locale: HisHopeLocale): string { return locale.split('-')[0].slice(0, 2).toUpperCase(); }

  change(locale: HisHopeLocale): void {
    this.i18n.setLocale(locale);
    this.localizationApi.load(locale).subscribe();
    this.localizationApi.savePreferredLanguage(locale).subscribe();
    this.open.set(false);
    this.localeChange.emit(locale);
    this.focusTrigger();
  }

  toggle(): void {
    this.open.update(value => !value);
    if (!this.open()) return;
    queueMicrotask(() => this.focusSelectedOption());
  }

  closeOnPointerLeave(): void { this.open.set(false); }

  onTriggerKeydown(event: KeyboardEvent): void {
    if (event.key === 'ArrowDown' || event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      if (!this.open()) this.open.set(true);
      queueMicrotask(() => this.focusSelectedOption());
    }
  }

  onMenuKeydown(event: KeyboardEvent): void {
    const buttons = this.optionButtons?.toArray() ?? [];
    if (!buttons.length) return;
    const current = document.activeElement as HTMLButtonElement;
    const currentIndex = buttons.findIndex(button => button.nativeElement === current);
    let nextIndex = currentIndex;
    if (event.key === 'ArrowDown') nextIndex = (currentIndex + 1) % buttons.length;
    else if (event.key === 'ArrowUp') nextIndex = (currentIndex - 1 + buttons.length) % buttons.length;
    else if (event.key === 'Home') nextIndex = 0;
    else if (event.key === 'End') nextIndex = buttons.length - 1;
    else if (event.key === 'Escape') { event.preventDefault(); this.open.set(false); this.focusTrigger(); return; }
    else if (event.key === 'Tab') { this.open.set(false); return; }
    else return;
    event.preventDefault();
    buttons[nextIndex].nativeElement.focus();
  }

  @HostListener('document:keydown.escape') closeOnEscape(): void {
    if (!this.open()) return;
    this.open.set(false);
    this.focusTrigger();
  }

  private focusSelectedOption(): void {
    const buttons = this.optionButtons?.toArray() ?? [];
    const index = this.locales().findIndex(locale => locale.code === this.i18n.locale());
    buttons[index < 0 ? 0 : index]?.nativeElement.focus();
  }

  private focusTrigger(): void { this.triggerButtons?.first?.nativeElement.focus(); }
}

export interface HisHopeLanguageOption { code: HisHopeLocale; label: string; icon: string; flag?: string; }

function flagData(svg: string): string { return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`; }

// Inline assets keep the switcher deterministic across operating systems and offline deployments.
const HIS_HOPE_FLAG_SOURCES: Record<string, string> = {
  'vi-vn': flagData('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 30 20"><rect width="30" height="20" fill="#da251d"/><path fill="#ffed00" d="m15 3 1.6 4.6 4.8.1-3.8 2.9 1.4 4.7-4-2.7-4 2.7 1.4-4.7-3.8-2.9 4.8-.1z"/></svg>'),
  en: flagData('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 30 20"><rect width="30" height="20" fill="#123b7a"/><path stroke="#fff" stroke-width="5" d="M0 0 30 20M30 0 0 20M15 0v20M0 10h30"/><path stroke="#c8102e" stroke-width="2.4" d="M0 0 30 20M30 0 0 20M15 0v20M0 10h30"/></svg>'),
  'ja-jp': flagData('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 30 20"><rect width="30" height="20" fill="#fff"/><circle cx="15" cy="10" r="6" fill="#bc002d"/></svg>'),
  'zh-cn': flagData('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 30 20"><rect width="30" height="20" fill="#de2910"/><path fill="#ffde00" d="m6 3 1 2.1 2.3.2-1.7 1.5.5 2.2L6 7.8 4 9l.5-2.2-1.7-1.5 2.3-.2z"/></svg>'),
  'fr-fr': flagData('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 30 20"><path fill="#0055a4" d="M0 0h10v20H0z"/><path fill="#fff" d="M10 0h10v20H10z"/><path fill="#ef4135" d="M20 0h10v20H20z"/></svg>'),
  'de-de': flagData('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 30 20"><path fill="#000" d="M0 0h30v6.7H0z"/><path fill="#d00" d="M0 6.7h30v6.6H0z"/><path fill="#ffce00" d="M0 13.3h30V20H0z"/></svg>'),
};
