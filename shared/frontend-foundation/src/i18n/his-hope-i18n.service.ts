import { DOCUMENT, isPlatformBrowser } from "@angular/common";
import { Injectable, PLATFORM_ID, inject, signal } from "@angular/core";
import { HisHopeTranslationDictionary } from "../contracts/his-hope-ui-contracts";
import { hisHopeEn } from "./dictionaries/en";
import { hisHopeViVN } from "./dictionaries/vi-vn";

export type HisHopeLocale = string;

@Injectable({ providedIn: "root" })
export class HisHopeI18nService {
  readonly locale = signal<HisHopeLocale>("vi-VN");
  readonly timeZone = signal<string>("Asia/Ho_Chi_Minh");
  readonly currency = signal<string>("VND");
  private dictionary: HisHopeTranslationDictionary = hisHopeViVN;
  private readonly remoteDictionaries: Record<string, Record<string, string>> = {};
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);
  private syncChannel?: BroadcastChannel;

  readonly dictionaries: Record<string, HisHopeTranslationDictionary> = {
    "vi-VN": hisHopeViVN,
    en: hisHopeEn,
  };

  constructor() {
    if (isPlatformBrowser(this.platformId) && "BroadcastChannel" in window) {
      this.syncChannel = new BroadcastChannel("his-hope-preferences");
      this.syncChannel.onmessage = ({ data }) => {
        if (data?.type === "locale" && typeof data.locale === "string")
          this.setLocale(data.locale, false);
      };
    }
    this.restore();
    this.restoreRegionalPreferences();
  }

  setLocale(locale: HisHopeLocale, broadcast = true): void {
    const dictionary = this.dictionaries[locale];
    if (!dictionary) return;
    this.locale.set(locale);
    this.dictionary = dictionary;
    this.applyDocumentLocale(locale);
    if (isPlatformBrowser(this.platformId)) {
      localStorage.setItem("hh-locale", locale);
      if (broadcast) this.syncChannel?.postMessage({ type: "locale", locale });
    }
  }

  registerTranslations(locale: string, translations: Record<string, string>): void {
    this.remoteDictionaries[locale] = { ...this.remoteDictionaries[locale], ...translations };
  }

  apiLocale(): string { return this.locale() === "en" ? "en-US" : this.locale(); }

  setTimeZone(timeZone: string): void {
    if (!timeZone.trim()) return;
    this.timeZone.set(timeZone);
    if (isPlatformBrowser(this.platformId)) localStorage.setItem("hh-timezone", timeZone);
  }

  setCurrency(currency: string): void {
    const normalized = currency.trim().toUpperCase();
    if (!/^[A-Z]{3}$/.test(normalized)) return;
    this.currency.set(normalized);
    if (isPlatformBrowser(this.platformId)) localStorage.setItem("hh-currency", normalized);
  }

  registerLocale(
    locale: HisHopeLocale,
    dictionary: HisHopeTranslationDictionary,
  ): void {
    this.dictionaries[locale] = dictionary;
  }

  restore(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const stored = localStorage.getItem("hh-locale");
    const documentLocale = this.document.documentElement.lang;
    const locale =
      stored && this.dictionaries[stored]
        ? stored
        : documentLocale.toLowerCase().startsWith("en")
          ? "en"
          : "vi-VN";
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

  t(
    key: string,
    fallback = key,
    params: Record<string, string | number> = {},
  ): string {
    const remoteValue = this.remoteDictionaries[this.apiLocale()]?.[key];
    if (remoteValue) return this.interpolate(remoteValue, params);
    const value = key
      .split(".")
      .reduce<unknown>(
        (current, part) =>
          current && typeof current === "object"
            ? (current as HisHopeTranslationDictionary)[part]
            : undefined,
        this.dictionary,
      );
    return Object.entries(params).reduce(
      (result, [name, replacement]) =>
        result.split(`{{${name}}}`).join(String(replacement)),
      typeof value === "string" ? value : fallback,
    );
  }

  /** Locale-aware number formatting (currency, percent, plain) via `Intl`. */
  formatNumber(value: number, options?: Intl.NumberFormatOptions): string {
    return new Intl.NumberFormat(this.locale(), options).format(value);
  }

  /** Locale-aware date/time formatting via `Intl`. */
  formatDate(
    value: Date | number | string,
    options?: Intl.DateTimeFormatOptions,
  ): string {
    const date = value instanceof Date ? value : new Date(value);
    return new Intl.DateTimeFormat(this.apiLocale(), { timeZone: this.timeZone(), ...options }).format(date);
  }

  formatCurrency(value: number, currency = this.currency()): string {
    return new Intl.NumberFormat(this.apiLocale(), { style: "currency", currency }).format(value);
  }

  formatDateTime(value: Date | number | string, options: Intl.DateTimeFormatOptions = {}): string {
    return this.formatDate(value, { dateStyle: "medium", timeStyle: "short", ...options });
  }

  /** Picks the plural-form key ("one"/"other"/...) via `Intl.PluralRules`, then
   *  translates `${key}.${form}` \u2014 e.g. `t.plural('patients.count', 1)` looks up
   *  `patients.count.one`, falling back to `patients.count.other`. */
  plural(
    key: string,
    count: number,
    params: Record<string, string | number> = {},
  ): string {
    const form = new Intl.PluralRules(this.locale()).select(count);
    const withCount = { count, ...params };
    const specific = this.t(`${key}.${form}`, "");
    if (specific) return this.interpolate(specific, withCount);
    return this.interpolate(this.t(`${key}.other`, key), withCount);
  }

  private interpolate(
    value: string,
    params: Record<string, string | number>,
  ): string {
    return Object.entries(params).reduce(
      (result, [name, replacement]) =>
        result.split(`{{${name}}}`).join(String(replacement)),
      value,
    );
  }

  private applyDocumentLocale(locale: HisHopeLocale): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.document.documentElement.lang = locale;
    this.document.documentElement.dataset["locale"] = locale;
  }


  private restoreRegionalPreferences(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const timeZone = localStorage.getItem("hh-timezone") || Intl.DateTimeFormat().resolvedOptions().timeZone;
    const currency = localStorage.getItem("hh-currency");
    if (timeZone) this.timeZone.set(timeZone);
    if (currency && /^[A-Z]{3}$/i.test(currency)) this.currency.set(currency.toUpperCase());
  }
}
