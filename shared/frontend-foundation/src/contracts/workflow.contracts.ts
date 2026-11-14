export interface HisHopeWorkflowStepDefinition {
  key: string;
  i18nGroup: string;
}

export interface HisHopeWorkflowDefinition {
  id?: string;
  steps: readonly HisHopeWorkflowStepDefinition[];
  /** Maps runtime status values to a canonical step key in `steps`. */
  statusAliases?: Readonly<Record<string, string>>;
  terminalStatuses?: readonly string[];
}

export interface HisHopeWorkflowStepView {
  key: string;
  label: string;
}

export type HisHopeWorkflowStepState = "complete" | "current" | "upcoming" | "cancelled";

export interface HisHopeWorkflowStepRenderModel extends HisHopeWorkflowStepView {
  state: HisHopeWorkflowStepState;
}
