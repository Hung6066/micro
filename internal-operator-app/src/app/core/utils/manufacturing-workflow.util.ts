import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import {
  buildManufacturingWorkflowRenderModel,
  buildManufacturingWorkflowSteps,
} from "@his-hope/frontend-foundation/domain";
import type {
  HisHopeManufacturingWorkflowEntityType,
  HisHopeWorkflowStepRenderModel,
} from "@his-hope/frontend-foundation/contracts";
import { portalEnumLabel } from "./portal-label.util";

type WorkflowLabelGroup = Parameters<typeof portalEnumLabel>[1];

function workflowLabelFor(
  i18n: HisHopeI18nService,
  group: string,
  key: string,
): string {
  return portalEnumLabel(i18n, group as WorkflowLabelGroup, key);
}

export function buildEntityWorkflowSteps(
  i18n: HisHopeI18nService,
  entityType: HisHopeManufacturingWorkflowEntityType,
  status: string,
): HisHopeWorkflowStepRenderModel[] {
  i18n.locale();
  return buildManufacturingWorkflowRenderModel(entityType, status, (group, key) =>
    workflowLabelFor(i18n, group, key),
  );
}

export function buildReferenceWorkflowSteps(
  i18n: HisHopeI18nService,
  entityType: HisHopeManufacturingWorkflowEntityType,
): HisHopeWorkflowStepRenderModel[] {
  i18n.locale();
  return buildManufacturingWorkflowSteps(entityType, (group, key) =>
    workflowLabelFor(i18n, group, key),
  ).map((step) => ({ ...step, state: "upcoming" as const }));
}
