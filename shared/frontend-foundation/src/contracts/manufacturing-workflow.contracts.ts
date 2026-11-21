import type { HisHopeWorkflowDefinition } from "./workflow.contracts";

export type HisHopeManufacturingWorkflowEntityType =
  | "production-order"
  | "production-batch"
  | "purchase-order"
  | "deviation"
  | "capa"
  | "quality-inspection"
  | "quality-sample"
  | "inspection-plan"
  | "recipe"
  | "product-specification";

export interface HisHopeManufacturingWorkflowDefinition extends HisHopeWorkflowDefinition {
  entityType: HisHopeManufacturingWorkflowEntityType;
}

export interface HisHopeWorkflowStepDefinitionDto {
  key: string;
  i18nGroup: string;
}

export interface HisHopeManufacturingWorkflowDefinitionDto {
  entityType: HisHopeManufacturingWorkflowEntityType;
  steps: readonly HisHopeWorkflowStepDefinitionDto[];
  statusAliases?: Readonly<Record<string, string>>;
  terminalStatuses?: readonly string[];
}
