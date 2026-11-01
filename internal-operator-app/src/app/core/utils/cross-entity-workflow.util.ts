import type {
  HisHopeCrossEntityWorkflowStepDto,
  HisHopeCrossEntityWorkflowTraceDto,
} from "@his-hope/frontend-foundation/contracts";
import type { HisHopeWorkflowStepRenderModel } from "@his-hope/frontend-foundation/contracts";
import type { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { portalEnumLabel } from "./portal-label.util";

const STEP_LABEL_KEYS: Record<string, { key: string; fallback: string }> = {
  "purchase-order": { key: "customerPortal.crossWorkflowPurchaseOrder", fallback: "Purchase order" },
  lot: { key: "customerPortal.crossWorkflowInboundLot", fallback: "Inbound lot" },
  "quality-inspection": { key: "customerPortal.crossWorkflowRawQc", fallback: "Raw material QC" },
  "production-batch": { key: "customerPortal.crossWorkflowProductionBatch", fallback: "Production batch" },
  "output-lot": { key: "customerPortal.crossWorkflowOutputLot", fallback: "Finished lot" },
  "finished-qc": { key: "customerPortal.crossWorkflowFinishedQc", fallback: "Finished goods QC" },
};

function stepLabel(i18n: HisHopeI18nService, key: string): string {
  const entry = STEP_LABEL_KEYS[key] ?? { key: "customerPortal.crossWorkflowStep", fallback: "Step" };
  return i18n.t(entry.key, entry.fallback);
}

function statusLabel(i18n: HisHopeI18nService, entityType: string, status: string): string {
  switch (entityType) {
    case "purchase-order":
      return portalEnumLabel(i18n, "purchaseOrderStatus", status);
    case "production-batch":
      return portalEnumLabel(i18n, "productionBatchStatus", status);
    case "quality-inspection":
      return portalEnumLabel(i18n, "qualityInspectionStatus", status);
    case "lot":
      return portalEnumLabel(i18n, "disposition", status);
    default:
      return status;
  }
}

export function mapCrossEntityWorkflowToStepper(
  trace: HisHopeCrossEntityWorkflowTraceDto,
  i18n: HisHopeI18nService,
): HisHopeWorkflowStepRenderModel[] {
  i18n.locale();
  return trace.steps.map((step) => toRenderModel(step, i18n));
}

function toRenderModel(
  step: HisHopeCrossEntityWorkflowStepDto,
  i18n: HisHopeI18nService,
): HisHopeWorkflowStepRenderModel {
  const phase = stepLabel(i18n, step.key);
  const status = statusLabel(i18n, step.entityType, step.status);
  return {
    key: step.key,
    label: `${phase}: ${step.title} · ${status}`,
    state: step.state,
  };
}
