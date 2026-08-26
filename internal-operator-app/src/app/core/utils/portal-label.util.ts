import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";

export function portalEnumLabel(
  i18n: HisHopeI18nService,
  group:
    | "disposition"
    | "productionOrderStatus"
    | "productionBatchStatus"
    | "purchaseOrderStatus"
    | "severity",
  value: string,
): string {
  i18n.locale();
  return i18n.t(`customerPortal.${group}.${value}`, value);
}
