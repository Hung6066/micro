import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";

export function portalEnumLabel(
  i18n: HisHopeI18nService,
  group:
    | "disposition"
    | "productionOrderStatus"
    | "productionBatchStatus"
    | "purchaseOrderStatus"
    | "qualityInspectionStatus"
    | "qualitySampleDisposition"
    | "deviationStatus"
    | "capaStatus"
    | "supplierApprovalStatus"
    | "quotationStatus"
    | "maintenanceStatus"
    | "qualityTestResult"
    | "reservationStatus"
    | "salesOrderStatus"
    | "forecastSource"
    | "severity"
    | "governanceLifecycleStatus",
  value: string,
): string {
  i18n.locale();
  return i18n.t(`customerPortal.${group}.${value}`, value);
}
