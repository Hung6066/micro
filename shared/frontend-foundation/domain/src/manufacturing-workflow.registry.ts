import type {
  HisHopeManufacturingWorkflowDefinition,
  HisHopeManufacturingWorkflowEntityType,
} from "@his-hope/frontend-foundation/contracts";
import type {
  HisHopeWorkflowStepRenderModel,
  HisHopeWorkflowStepView,
} from "@his-hope/frontend-foundation/contracts";
import {
  buildWorkflowRenderModel,
  buildWorkflowSteps,
  resolveWorkflowStepIndex,
} from "./workflow.util";

const PRODUCTION_ORDER_WORKFLOW: HisHopeManufacturingWorkflowDefinition = {
  entityType: "production-order",
  steps: [
    { key: "Draft", i18nGroup: "productionOrderStatus" },
    { key: "Planned", i18nGroup: "productionOrderStatus" },
    { key: "Released", i18nGroup: "productionOrderStatus" },
    { key: "InProgress", i18nGroup: "productionOrderStatus" },
    { key: "Completed", i18nGroup: "productionOrderStatus" },
  ],
  statusAliases: { Open: "InProgress" },
  terminalStatuses: ["Cancelled"],
};

const PRODUCTION_BATCH_WORKFLOW: HisHopeManufacturingWorkflowDefinition = {
  entityType: "production-batch",
  steps: [
    { key: "Created", i18nGroup: "productionBatchStatus" },
    { key: "Started", i18nGroup: "productionBatchStatus" },
    { key: "Completed", i18nGroup: "productionBatchStatus" },
  ],
  statusAliases: {
    Paused: "Started",
    InProgress: "Started",
    AwaitingQA: "Started",
    Planned: "Created",
    Released: "Created",
  },
  terminalStatuses: ["Cancelled"],
};

const PURCHASE_ORDER_WORKFLOW: HisHopeManufacturingWorkflowDefinition = {
  entityType: "purchase-order",
  steps: [
    { key: "Draft", i18nGroup: "purchaseOrderStatus" },
    { key: "Approved", i18nGroup: "purchaseOrderStatus" },
    { key: "PartiallyReceived", i18nGroup: "purchaseOrderStatus" },
    { key: "Closed", i18nGroup: "purchaseOrderStatus" },
  ],
  statusAliases: {
    Submitted: "Draft",
    Ordered: "Approved",
    Received: "PartiallyReceived",
  },
  terminalStatuses: ["Cancelled"],
};

const DEVIATION_WORKFLOW: HisHopeManufacturingWorkflowDefinition = {
  entityType: "deviation",
  steps: [
    { key: "Requested", i18nGroup: "deviationStatus" },
    { key: "Approved", i18nGroup: "deviationStatus" },
    { key: "Closed", i18nGroup: "deviationStatus" },
  ],
  terminalStatuses: ["Rejected"],
};

const CAPA_WORKFLOW: HisHopeManufacturingWorkflowDefinition = {
  entityType: "capa",
  steps: [
    { key: "Open", i18nGroup: "capaStatus" },
    { key: "InProgress", i18nGroup: "capaStatus" },
    { key: "Verified", i18nGroup: "capaStatus" },
    { key: "Closed", i18nGroup: "capaStatus" },
  ],
};

const QUALITY_INSPECTION_WORKFLOW: HisHopeManufacturingWorkflowDefinition = {
  entityType: "quality-inspection",
  steps: [
    { key: "Pending", i18nGroup: "qualityInspectionStatus" },
    { key: "Pass", i18nGroup: "qualityInspectionStatus" },
  ],
  terminalStatuses: ["Fail", "Rejected"],
};

const QUALITY_SAMPLE_WORKFLOW: HisHopeManufacturingWorkflowDefinition = {
  entityType: "quality-sample",
  steps: [
    { key: "Pending", i18nGroup: "qualitySampleDisposition" },
    { key: "Released", i18nGroup: "qualitySampleDisposition" },
  ],
  statusAliases: { Accepted: "Released" },
  terminalStatuses: ["Rejected"],
};

const INSPECTION_PLAN_WORKFLOW: HisHopeManufacturingWorkflowDefinition = {
  entityType: "inspection-plan",
  steps: [
    { key: "Draft", i18nGroup: "governanceLifecycleStatus" },
    { key: "Approved", i18nGroup: "governanceLifecycleStatus" },
  ],
  statusAliases: { Submitted: "Draft" },
};

const RECIPE_WORKFLOW: HisHopeManufacturingWorkflowDefinition = {
  entityType: "recipe",
  steps: [
    { key: "Draft", i18nGroup: "governanceLifecycleStatus" },
    { key: "Submitted", i18nGroup: "governanceLifecycleStatus" },
    { key: "Approved", i18nGroup: "governanceLifecycleStatus" },
    { key: "Retired", i18nGroup: "governanceLifecycleStatus" },
  ],
};

const PRODUCT_SPECIFICATION_WORKFLOW: HisHopeManufacturingWorkflowDefinition = {
  entityType: "product-specification",
  steps: [
    { key: "Draft", i18nGroup: "governanceLifecycleStatus" },
    { key: "Approved", i18nGroup: "governanceLifecycleStatus" },
    { key: "Retired", i18nGroup: "governanceLifecycleStatus" },
  ],
};

const WORKFLOW_REGISTRY: Record<
  HisHopeManufacturingWorkflowEntityType,
  HisHopeManufacturingWorkflowDefinition
> = {
  "production-order": PRODUCTION_ORDER_WORKFLOW,
  "production-batch": PRODUCTION_BATCH_WORKFLOW,
  "purchase-order": PURCHASE_ORDER_WORKFLOW,
  deviation: DEVIATION_WORKFLOW,
  capa: CAPA_WORKFLOW,
  "quality-inspection": QUALITY_INSPECTION_WORKFLOW,
  "quality-sample": QUALITY_SAMPLE_WORKFLOW,
  "inspection-plan": INSPECTION_PLAN_WORKFLOW,
  recipe: RECIPE_WORKFLOW,
  "product-specification": PRODUCT_SPECIFICATION_WORKFLOW,
};

export function getManufacturingWorkflowDefinition(
  entityType: HisHopeManufacturingWorkflowEntityType,
): HisHopeManufacturingWorkflowDefinition {
  return WORKFLOW_REGISTRY[entityType];
}

export function listManufacturingWorkflowEntityTypes(): HisHopeManufacturingWorkflowEntityType[] {
  return Object.keys(WORKFLOW_REGISTRY) as HisHopeManufacturingWorkflowEntityType[];
}

export function buildManufacturingWorkflowSteps(
  entityType: HisHopeManufacturingWorkflowEntityType,
  labelFor: (group: string, key: string) => string,
): HisHopeWorkflowStepView[] {
  return buildWorkflowSteps(getManufacturingWorkflowDefinition(entityType), labelFor);
}

export function resolveManufacturingWorkflowStepIndex(
  entityType: HisHopeManufacturingWorkflowEntityType,
  currentStatus: string,
): number {
  return resolveWorkflowStepIndex(getManufacturingWorkflowDefinition(entityType), currentStatus);
}

export function buildManufacturingWorkflowRenderModel(
  entityType: HisHopeManufacturingWorkflowEntityType,
  currentStatus: string,
  labelFor: (group: string, key: string) => string,
): HisHopeWorkflowStepRenderModel[] {
  return buildWorkflowRenderModel(
    getManufacturingWorkflowDefinition(entityType),
    currentStatus,
    labelFor,
  );
}
