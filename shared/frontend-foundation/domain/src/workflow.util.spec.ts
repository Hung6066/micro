import type { HisHopeWorkflowDefinition } from "@his-hope/frontend-foundation/contracts";
import {
  buildWorkflowRenderModel,
  buildWorkflowSteps,
  resolveWorkflowStepIndex,
} from "./workflow.util";

describe("workflow.util", () => {
  const definition: HisHopeWorkflowDefinition = {
    id: "sample",
    steps: [
      { key: "Draft", i18nGroup: "status" },
      { key: "Approved", i18nGroup: "status" },
      { key: "Closed", i18nGroup: "status" },
    ],
    statusAliases: { Submitted: "Draft" },
    terminalStatuses: ["Cancelled"],
  };

  const labelFor = (_group: string, key: string) => key;

  it("builds step labels from the workflow definition", () => {
    expect(buildWorkflowSteps(definition, labelFor).map((step) => step.key)).toEqual([
      "Draft",
      "Approved",
      "Closed",
    ]);
  });

  it("resolves aliased statuses to canonical step indexes", () => {
    expect(resolveWorkflowStepIndex(definition, "Submitted")).toBe(0);
    expect(resolveWorkflowStepIndex(definition, "Approved")).toBe(1);
  });

  it("marks terminal statuses as cancelled in the render model", () => {
    const model = buildWorkflowRenderModel(definition, "Cancelled", labelFor);
    expect(model.some((step) => step.state === "cancelled")).toBeTrue();
  });
});
