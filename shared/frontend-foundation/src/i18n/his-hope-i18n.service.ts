import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { HisHopeTranslationDictionary } from '../contracts/his-hope-ui-contracts';
import { hisHopeEn } from './dictionaries/en';
import { hisHopeViVN } from './dictionaries/vi-vn';

export type HisHopeLocale = string;

@Injectable({ providedIn: 'root' })
export class HisHopeI18nService {
  readonly locale = signal<HisHopeLocale>('vi-VN');
  private dictionary: HisHopeTranslationDictionary = hisHopeViVN;
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private syncChannel?: BroadcastChannel;

  readonly dictionaries: Record<string, HisHopeTranslationDictionary> = {
    'vi-VN': hisHopeViVN,
    en: hisHopeEn,
  };

  constructor() {
    if (isPlatformBrowser(this.platformId) && 'BroadcastChannel' in window) {
      this.syncChannel = new BroadcastChannel('his-hope-preferences');
      this.syncChannel.onmessage = ({ data }) => {
        if (data?.type === 'locale' && typeof data.locale === 'string') this.setLocale(data.locale, false);
      };
    }
    this.restore();
  }

  setLocale(locale: HisHopeLocale, broadcast = true): void {
    const dictionary = this.dictionaries[locale];
    if (!dictionary) return;
    this.locale.set(locale);
    this.dictionary = dictionary;
    this.applyDocumentLocale(locale);
    if (isPlatformBrowser(this.platformId)) {
      localStorage.setItem('hh-locale', locale);
      if (broadcast) this.syncChannel?.postMessage({ type: 'locale', locale });
    }
  }

  registerLocale(locale: HisHopeLocale, dictionary: HisHopeTranslationDictionary): void {
    this.dictionaries[locale] = dictionary;
  }

  restore(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const stored = localStorage.getItem('hh-locale');
    const documentLocale = this.document.documentElement.lang;
    const locale = stored && this.dictionaries[stored]
      ? stored
      : documentLocale.toLowerCase().startsWith('en') ? 'en' : 'vi-VN';
    this.setLocale(locale, false);
  }

  configure(locale: string, dictionary?: HisHopeTranslationDictionary): void {
    if (dictionary) {
      this.locale.set(locale as HisHopeLocale);
      this.dictionary = dictionary;
      this.applyDocumentLocale(locale);
      return;
    }
    if (this.dictionaries[locale]) this.setLocale(locale);
  }

  t(key: string, fallback = key, params: Record<string, string | number> = {}): string {
    const value = key.split('.').reduce<unknown>((current, part) => current && typeof current === 'object' ? (current as HisHopeTranslationDictionary)[part] : undefined, this.dictionary);
    return Object.entries(params).reduce((result, [name, replacement]) => result.split(`{{${name}}}`).join(String(replacement)), typeof value === 'string' ? value : fallback);
  }

  private applyDocumentLocale(locale: HisHopeLocale): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.document.documentElement.lang = locale;
    this.document.documentElement.dataset['locale'] = locale;
  }
}
