export const HIS_HOPE_DEFAULT_LOCALE = "vi-VN";
export const HIS_HOPE_DEFAULT_TIME_ZONE = "Asia/Ho_Chi_Minh";
export const HIS_HOPE_DEFAULT_CURRENCY = "VND";

export interface HisHopeRegionalPreferences {
  readonly locale: string;
  readonly timeZone: string;
  readonly currency: string;
}

export function getHisHopeRegionalPreferences(locale?: string, currency = HIS_HOPE_DEFAULT_CURRENCY): HisHopeRegionalPreferences {
  const resolvedLocale = locale || (typeof Intl !== "undefined" ? Intl.DateTimeFormat().resolvedOptions().locale : HIS_HOPE_DEFAULT_LOCALE);
  const resolvedTimeZone = typeof Intl !== "undefined" ? Intl.DateTimeFormat().resolvedOptions().timeZone : HIS_HOPE_DEFAULT_TIME_ZONE;
  return { locale: resolvedLocale, timeZone: resolvedTimeZone || HIS_HOPE_DEFAULT_TIME_ZONE, currency: /^[A-Z]{3}$/i.test(currency) ? currency.toUpperCase() : HIS_HOPE_DEFAULT_CURRENCY };
}

export function formatHisHopeMoney(value: number, preferences = getHisHopeRegionalPreferences()): string {
  return new Intl.NumberFormat(preferences.locale, { style: "currency", currency: preferences.currency }).format(value);
}

export function formatHisHopeDateTime(value: Date | number | string, preferences = getHisHopeRegionalPreferences()): string {
  return new Intl.DateTimeFormat(preferences.locale, { timeZone: preferences.timeZone, dateStyle: "medium", timeStyle: "short" }).format(new Date(value));
}
