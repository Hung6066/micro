import type { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";

/** Resolve backend enum values through the shared manufacturing dictionary. */
export function manufacturingEnumLabel(i18n: HisHopeI18nService, enumName: string, value: string | null | undefined): string {
  const normalized = value?.trim();
  if (!normalized) return "";
  return i18n.t(`manufacturing.${enumName}.${normalized}`, normalized);
}
