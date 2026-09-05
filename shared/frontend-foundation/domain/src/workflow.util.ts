import type {
  HisHopeWorkflowDefinition,
  HisHopeWorkflowStepRenderModel,
  HisHopeWorkflowStepView,
} from "@his-hope/frontend-foundation/contracts";

export function buildWorkflowSteps(
  definition: HisHopeWorkflowDefinition,
  labelFor: (group: string, key: string) => string,
): HisHopeWorkflowStepView[] {
  return definition.steps.map((step) => ({
    key: step.key,
    label: labelFor(step.i18nGroup, step.key),
  }));
}

export function resolveWorkflowStepIndex(
  definition: HisHopeWorkflowDefinition,
  currentStatus: string,
): number {
  if (definition.terminalStatuses?.includes(currentStatus)) {
    return -1;
  }

  const canonical = definition.statusAliases?.[currentStatus] ?? currentStatus;
  return definition.steps.findIndex((step) => step.key === canonical);
}

export function buildWorkflowRenderModel(
  definition: HisHopeWorkflowDefinition,
  currentStatus: string,
  labelFor: (group: string, key: string) => string,
): HisHopeWorkflowStepRenderModel[] {
  const steps = buildWorkflowSteps(definition, labelFor);
  const currentIndex = resolveWorkflowStepIndex(definition, currentStatus);
  const cancelled = definition.terminalStatuses?.includes(currentStatus) ?? false;

  return steps.map((step, index) => {
    let state: HisHopeWorkflowStepRenderModel["state"] = "upcoming";
    if (cancelled) {
      state =
        index === Math.max(currentIndex, 0)
          ? "cancelled"
          : index < Math.max(currentIndex, 0)
            ? "complete"
            : "upcoming";
    } else if (currentIndex < 0) {
      state = "upcoming";
    } else if (index < currentIndex) {
      state = "complete";
    } else if (index === currentIndex) {
      state = "current";
    }

    return { ...step, state };
  });
}
