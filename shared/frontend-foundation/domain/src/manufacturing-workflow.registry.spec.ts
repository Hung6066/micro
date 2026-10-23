import type {
  HisHopeManufacturingWorkflowEntityType,
  HisHopeWorkflowStepRenderModel,
} from "@his-hope/frontend-foundation/contracts";
import {
  buildManufacturingWorkflowRenderModel,
  buildManufacturingWorkflowSteps,
  getManufacturingWorkflowDefinition,
  resolveManufacturingWorkflowStepIndex,
} from "./manufacturing-workflow.registry";

describe("manufacturing-workflow.registry", () => {
  const labelFor = (_group: string, key: string) => key;

  it("maps production batch paused status to the started step", () => {
    expect(resolveManufacturingWorkflowStepIndex("production-batch", "Paused")).toBe(1);
  });

  it("maps purchase order received status to partially received", () => {
    expect(resolveManufacturingWorkflowStepIndex("purchase-order", "Received")).toBe(2);
  });

  it("marks cancelled purchase orders as terminal", () => {
    const model = buildManufacturingWorkflowRenderModel(
      "purchase-order",
      "Cancelled",
      labelFor,
    );
    expect(model.some((step) => step.state === "cancelled")).toBeTrue();
  });

  it("exposes stable workflow definitions for each entity type", () => {
    const types: HisHopeManufacturingWorkflowEntityType[] = [
      "production-order",
      "production-batch",
      "purchase-order",
      "deviation",
      "capa",
      "quality-inspection",
      "recipe",
      "product-specification",
    ];

    for (const entityType of types) {
      const definition = getManufacturingWorkflowDefinition(entityType);
      expect(definition.steps.length).toBeGreaterThan(2);
      expect(buildManufacturingWorkflowSteps(entityType, labelFor).length).toBe(
        definition.steps.length,
      );
    }
  });

  it("builds render models with completed steps before the current one", () => {
    const model: HisHopeWorkflowStepRenderModel[] = buildManufacturingWorkflowRenderModel(
      "production-order",
      "Released",
      labelFor,
    );

    expect(model[0]?.state).toBe("complete");
    expect(model[1]?.state).toBe("complete");
    expect(model[2]?.state).toBe("current");
    expect(model[3]?.state).toBe("upcoming");
  });
});
